using Bit.Core.Auth.Repositories;
using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Identity.IdentityServer;
using Bit.SharedWeb.Utilities;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using StackExchange.Redis;

namespace Bit.Identity.Utilities;

public static class ServiceCollectionExtensions
{
    public static IIdentityServerBuilder AddCustomIdentityServerServices(this IServiceCollection services,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        services.AddTransient<IDiscoveryResponseGenerator, DiscoveryResponseGenerator>();

        services.AddSingleton<StaticClientStore>();
        services.AddTransient<IAuthorizationCodeStore, AuthorizationCodeStore>();
        services.AddTransient<IUserDecryptionOptionsBuilder, UserDecryptionOptionsBuilder>();

        var issuerUri = new Uri(globalSettings.BaseServiceUri.InternalIdentity);
        var identityServerBuilder = services
            .AddIdentityServer(options =>
            {
                options.LicenseKey = globalSettings.IdentityServer.LicenseKey;
                options.Endpoints.EnableIntrospectionEndpoint = false;
                options.Endpoints.EnableEndSessionEndpoint = false;
                options.Endpoints.EnableUserInfoEndpoint = false;
                options.Endpoints.EnableCheckSessionEndpoint = false;
                options.Endpoints.EnableTokenRevocationEndpoint = false;
                options.IssuerUri = $"{issuerUri.Scheme}://{issuerUri.Host}";
                options.Caching.ClientStoreExpiration = new TimeSpan(0, 5, 0);
                if (env.IsDevelopment())
                {
                    options.Authentication.CookieSameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                }
                options.InputLengthRestrictions.UserName = 256;
                options.KeyManagement.Enabled = false;
            })
            .AddInMemoryCaching()
            .AddInMemoryApiResources(ApiResources.GetApiResources())
            .AddInMemoryApiScopes(ApiScopes.GetApiScopes())
            .AddClientStoreCache<ClientStore>()
            .AddCustomTokenRequestValidator<CustomTokenRequestValidator>()
            .AddProfileService<ProfileService>()
            .AddResourceOwnerValidator<ResourceOwnerPasswordValidator>()
            .AddClientStore<ClientStore>()
            .AddIdentityServerCertificate(env, globalSettings)
            .AddExtensionGrantValidator<WebAuthnGrantValidator>();

        if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.CosmosConnectionString))
        {
            services.AddSingleton<IPersistedGrantStore>(sp => BuildCosmosGrantStore(sp, globalSettings));
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.RedisConnectionString))
        {
            services.AddSingleton<IPersistedGrantStore>(sp => BuildRedisGrantStore(sp, globalSettings));
        }
        else
        {
            services.AddTransient<IPersistedGrantStore>(sp => BuildSqlGrantStore(sp));
        }

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }

    private static PersistedGrantStore BuildCosmosGrantStore(IServiceProvider sp, GlobalSettings globalSettings)
    {
        if (!CoreHelpers.SettingHasValue(globalSettings.IdentityServer.CosmosConnectionString))
        {
            throw new ArgumentException("No cosmos config string available.");
        }
        return new PersistedGrantStore(
            // TODO: Perhaps we want to evaluate moving this repo to DI as a keyed service singleton in .NET 8
            new Core.Auth.Repositories.Cosmos.GrantRepository(globalSettings),
            g => new Core.Auth.Models.Data.GrantItem(g),
            fallbackGrantStore: BuildRedisGrantStore(sp, globalSettings, true));
    }

    private static RedisPersistedGrantStore BuildRedisGrantStore(IServiceProvider sp,
        GlobalSettings globalSettings, bool allowNull = false)
    {
        if (!CoreHelpers.SettingHasValue(globalSettings.IdentityServer.RedisConnectionString))
        {
            if (allowNull)
            {
                return null;
            }
            throw new ArgumentException("No redis config string available.");
        }

        return new RedisPersistedGrantStore(
            // TODO: .NET 8 create a keyed service for this connection multiplexer and even PersistedGrantStore
            ConnectionMultiplexer.Connect(globalSettings.IdentityServer.RedisConnectionString),
            sp.GetRequiredService<ILogger<RedisPersistedGrantStore>>(),
            fallbackGrantStore: BuildSqlGrantStore(sp));
    }

    private static PersistedGrantStore BuildSqlGrantStore(IServiceProvider sp)
    {
        return new PersistedGrantStore(sp.GetRequiredService<IGrantRepository>(),
            g => new Core.Auth.Entities.Grant(g));
    }
}
