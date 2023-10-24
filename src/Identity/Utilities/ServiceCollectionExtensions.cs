using Bit.Core.IdentityServer;
using Bit.Core.Settings;
using Bit.Identity.IdentityServer;
using Bit.SharedWeb.Utilities;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;

namespace Bit.Identity.Utilities;

public static class ServiceCollectionExtensions
{
    public static IIdentityServerBuilder AddCustomIdentityServerServices(this IServiceCollection services,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        services.AddTransient<IDiscoveryResponseGenerator, DiscoveryResponseGenerator>();

        services.AddSingleton<StaticClientStore>();
        services.AddTransient<IAuthorizationCodeStore, AuthorizationCodeStore>();

        var issuerUri = new Uri(globalSettings.BaseServiceUri.InternalIdentity);
        var identityServerBuilder = services
            .AddIdentityServer(options =>
            {
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
            .AddPersistedGrantStore<PersistedGrantStore>()
            .AddClientStore<ClientStore>()
            .AddIdentityServerCertificate(env, globalSettings);

        services.AddTransient<ICorsPolicyService, CustomCorsPolicyService>();
        return identityServerBuilder;
    }
}
