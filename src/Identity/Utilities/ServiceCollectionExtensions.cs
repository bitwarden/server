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

        if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.StorageConnectionString))
        {
            // If we have table storage, prefer it

            services.AddSingleton<IPersistedGrantStore>(sp =>
            {
                var sqlFallbackStore = new PersistedGrantStore(
                    sp.GetRequiredService<IGrantRepository>(),
                    g => new Core.Auth.Entities.Grant(g));

                var redisFallbackStore = new RedisPersistedGrantStore(
                    // TODO: .NET 8 create a keyed service for this connection multiplexer and even PersistedGrantStore
                    ConnectionMultiplexer.Connect(globalSettings.IdentityServer.RedisConnectionString),
                    sp.GetRequiredService<ILogger<RedisPersistedGrantStore>>(),
                    sqlFallbackStore // Fallback grant store
                );

                return new FallbackPersistedGrantStore(
                    new Core.Auth.Repositories.TableStorage.GrantRepository(globalSettings),
                    g => new Core.Auth.Models.Data.GrantTableEntity(g),
                    redisFallbackStore);
            });
        }
        else if (CoreHelpers.SettingHasValue(globalSettings.IdentityServer.RedisConnectionString))
        {
            services.AddSingleton<IPersistedGrantStore>(sp =>
            {
                var sqlFallbackStore = new PersistedGrantStore(
                    sp.GetRequiredService<IGrantRepository>(),
                    g => new Core.Auth.Entities.Grant(g));

                return new RedisPersistedGrantStore(
                    // TODO: .NET 8 create a keyed service for this connection multiplexer and even PersistedGrantStore
                    ConnectionMultiplexer.Connect(globalSettings.IdentityServer.RedisConnectionString),
                    sp.GetRequiredService<ILogger<RedisPersistedGrantStore>>(),
                    sqlFallbackStore // Fallback grant store
                );
            });
        }
        else
        {
            // Use the original grant store
            services.AddTransient<IPersistedGrantStore>(sp =>
            {
                return new PersistedGrantStore(
                    sp.GetRequiredService<IGrantRepository>(),
                    g => new Core.Auth.Entities.Grant(g));
            });
        }

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }
}
