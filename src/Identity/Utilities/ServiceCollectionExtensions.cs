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
            services.AddSingleton<IPersistedGrantStore>(sp => BuildCosmosStore(sp, globalSettings));
        }
        if (globalSettings.IdentityServer.StorageConnectionStrings != null &&
            globalSettings.IdentityServer.StorageConnectionStrings.Length > 0 &&
            CoreHelpers.SettingHasValue(globalSettings.IdentityServer.StorageConnectionStrings[0]))
        {
            services.AddSingleton<IPersistedGrantStore>(sp => BuildTableStorageStore(sp, globalSettings));
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.RedisConnectionString))
        {
            services.AddSingleton<IPersistedGrantStore>(sp => BuildRedisStore(sp, globalSettings));
        }
        else
        {
            services.AddTransient<IPersistedGrantStore>(sp => BuildSqlStore(sp));
        }

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }

    private static PersistedGrantStore BuildCosmosStore(IServiceProvider sp, GlobalSettings globalSettings)
    {
        return new PersistedGrantStore(
            new Core.Auth.Repositories.Cosmos.GrantRepository(globalSettings),
            g => new Core.Auth.Models.Data.GrantItem(g),
            fallbackGrantStore: BuildRedisStore(sp, globalSettings));
    }

    private static PersistedGrantStore BuildTableStorageStore(IServiceProvider sp, GlobalSettings globalSettings)
    {
        return new PersistedGrantStore(
            new Core.Auth.Repositories.TableStorage.GrantRepository(globalSettings),
            g => new Core.Auth.Models.Data.GrantTableEntity(g),
            fallbackGrantStore: BuildRedisStore(sp, globalSettings));
    }

    private static RedisPersistedGrantStore BuildRedisStore(IServiceProvider sp, GlobalSettings globalSettings)
    {
        return new RedisPersistedGrantStore(
            // TODO: .NET 8 create a keyed service for this connection multiplexer and even PersistedGrantStore
            ConnectionMultiplexer.Connect(globalSettings.IdentityServer.RedisConnectionString),
            sp.GetRequiredService<ILogger<RedisPersistedGrantStore>>(),
            fallbackGrantStore: BuildSqlStore(sp));
    }

    private static PersistedGrantStore BuildSqlStore(IServiceProvider sp)
    {
        return new PersistedGrantStore(sp.GetRequiredService<IGrantRepository>(),
            g => new Core.Auth.Entities.Grant(g));
    }
}
