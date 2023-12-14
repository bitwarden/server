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

        if (CoreHelpers.SettingHasValue(globalSettings.Grants.RedisConnectionString))
        {
            // If we have redis, prefer it

            // Add the original persisted grant store via it's implementation type
            // so we can inject it right after.
            services.AddSingleton<PersistedGrantStore>();

            services.AddTransient<IPersistedGrantStore>(sp =>
            {
                return new RedisPersistedGrantStore(
                    // TODO: This should be done through DI where CM is a singleton
                    ConnectionMultiplexer.Connect(globalSettings.Grants.RedisConnectionString),
                    sp.GetRequiredService<ILogger<RedisPersistedGrantStore>>(),
                    sp.GetRequiredService<PersistedGrantStore>()
                );
            });
        }
        else
        {
            // Use the original grant store
            identityServerBuilder.AddPersistedGrantStore<PersistedGrantStore>();
        }

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }
}
