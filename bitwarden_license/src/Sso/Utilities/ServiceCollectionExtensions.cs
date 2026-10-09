// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Business.Sso;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.SharedWeb.Utilities;
using Bit.Sso.IdentityServer;
using Bit.Sso.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.ResponseHandling;
using Duende.IdentityServer.Stores;
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
        services.AddOidcBackchannelHttpClient(globalSettings);
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

    /// <summary>
    /// Registers the named HTTP client whose handler pipeline every OpenID Connect scheme uses for its
    /// backchannel requests (discovery metadata, JWKS, token and userinfo).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Those destinations come from the organization's own SSO configuration (Authority /
    /// MetadataAddress), and the discovery fetch is reachable without authentication via
    /// /sso/prevalidate, so on cloud the backchannel is an SSRF sink and is wrapped in the same
    /// SSRF protection applied to the other organization-controlled clients (webhooks, Icons).
    /// </para>
    /// <para>
    /// Self-hosted installations are excluded. Their IdP often runs on the same private network as
    /// the server, and the SSRF guard refuses every private, loopback and link-local address with no
    /// allowlist, so OIDC discovery and sign-in would fail. The accepted trade-off is that an
    /// organization admin on a self-hosted server can direct these requests at the operator's
    /// internal network.
    /// </para>
    /// </remarks>
    private static IServiceCollection AddOidcBackchannelHttpClient(this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        // Only the handler pipeline is used. The OpenID Connect post-configure step builds the
        // HttpClient around it and applies its own timeout, buffer limit and user agent, so
        // client-level settings configured on this registration have no effect.
        var builder = services.AddHttpClient(DynamicAuthenticationSchemeProvider.OidcBackchannelHttpClientName);

        if (!globalSettings.SelfHosted)
        {
            builder.AddSsrfProtection();
        }

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
                options.LicenseKey = globalSettings.IdentityServer.LicenseKey;
                options.Endpoints.EnablePushedAuthorizationEndpoint = false;
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
                options.KeyManagement.Enabled = false;
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

        // PM-23572
        // Register named FusionCache for SSO authorization code grants.
        // Provides separation of concerns and automatic Redis/in-memory negotiation
        // .AddInMemoryCaching should still persist above; this handles configuration caching, etc.,
        // and is separate from this keyed service, which only serves grant negotiation.
        services.AddExtendedCache(PersistedGrantsDistributedCacheConstants.CacheKey, globalSettings);

        // Store authorization codes in distributed cache for horizontal scaling
        // Uses named FusionCache which gracefully degrades to in-memory when Redis isn't configured
        services.AddSingleton<IPersistedGrantStore, DistributedCachePersistedGrantStore>();

        return identityServerBuilder;
    }
}
