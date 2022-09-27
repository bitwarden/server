using Bit.Core.Business.Sso;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Bit.Sso.IdentityServer;
using Bit.Sso.Models;
using IdentityServer4.Models;
using IdentityServer4.ResponseHandling;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Sustainsys.Saml2.AspNetCore2;

namespace Bit.Sso.Utilities;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSsoServices(this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        // SAML SP Configuration
        var samlEnvironment = new SamlEnvironment
        {
            SpSigningCertificate = CoreHelpers.GetIdentityServerCertificate(globalSettings),
        };
        services.AddSingleton(s => samlEnvironment);

        services.AddSingleton<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider,
            DynamicAuthenticationSchemeProvider>();
        // Oidc
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<OpenIdConnectOptions>,
            OpenIdConnectPostConfigureOptions>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitorCache<OpenIdConnectOptions>,
            ExtendedOptionsMonitorCache<OpenIdConnectOptions>>();
        // Saml2
        services.AddSingleton<Microsoft.Extensions.Options.IPostConfigureOptions<Saml2Options>,
            PostConfigureSaml2Options>();
        services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitorCache<Saml2Options>,
            ExtendedOptionsMonitorCache<Saml2Options>>();

        return services;
    }

    public static IIdentityServerBuilder AddSsoIdentityServerServices(this IServiceCollection services,
        IWebHostEnvironment env, GlobalSettings globalSettings)
    {
        services.AddTransient<IDiscoveryResponseGenerator, DiscoveryResponseGenerator>();

        var issuerUri = new Uri(globalSettings.BaseServiceUri.InternalSso);
        var identityServerBuilder = services
            .AddIdentityServer(options =>
            {
                options.IssuerUri = $"{issuerUri.Scheme}://{issuerUri.Host}";
                if (env.IsDevelopment())
                {
                    options.Authentication.CookieSameSiteMode = Microsoft.AspNetCore.Http.SameSiteMode.Unspecified;
                }
                else
                {
                    options.UserInteraction.ErrorUrl = "/Error";
                    options.UserInteraction.ErrorIdParameter = "errorId";
                }
                options.InputLengthRestrictions.UserName = 256;
            })
            .AddInMemoryCaching()
            .AddInMemoryClients(new List<Client>
            {
                new OidcIdentityClient(globalSettings)
            })
            .AddInMemoryIdentityResources(new List<IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile()
            })
            .AddIdentityServerCertificate(env, globalSettings);

        return identityServerBuilder;
    }
}
