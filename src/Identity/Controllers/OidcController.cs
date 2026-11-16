#nullable enable

using Bit.Core;
using Bit.Core.Auth.IdentityServer;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Services;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Bitwarden.Server.Sdk.Features;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Identity.Controllers;

/// <summary>
/// Interactive endpoints for the "Login with Bitwarden" OIDC partner flow. IdentityServer redirects
/// unauthenticated/unconsented authorize requests here; this controller routes org-SSO requests to the
/// existing <see cref="SsoController"/> and partner requests to the web vault, and completes the flow when
/// the web vault returns a signed consent token.
/// </summary>
[Route("oidc/[action]")]
public class OidcController : Controller
{
    private readonly IIdentityServerInteractionService _interaction;
    private readonly IOpenIdConnectPartnerStore _partnerStore;
    private readonly IDataProtectorTokenFactory<OAuthConsentTokenable> _consentTokenFactory;
    private readonly IOAuthConsentNonceStore _nonceStore;
    private readonly IFeatureService _featureService;
    private readonly GlobalSettings _globalSettings;
    private readonly ILogger<OidcController> _logger;

    public OidcController(
        IIdentityServerInteractionService interaction,
        IOpenIdConnectPartnerStore partnerStore,
        IDataProtectorTokenFactory<OAuthConsentTokenable> consentTokenFactory,
        IOAuthConsentNonceStore nonceStore,
        IFeatureService featureService,
        GlobalSettings globalSettings,
        ILogger<OidcController> logger)
    {
        _interaction = interaction;
        _partnerStore = partnerStore;
        _consentTokenFactory = consentTokenFactory;
        _nonceStore = nonceStore;
        _featureService = featureService;
        _globalSettings = globalSettings;
        _logger = logger;
    }

    /// <summary>
    /// Login dispatcher (IdentityServer <c>UserInteraction.LoginUrl</c>). Org-SSO requests (a
    /// <c>domain_hint</c> is present) are handed to the existing SSO flow; partner requests are redirected to
    /// the web vault consent page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Login(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context == null)
        {
            throw new InvalidOperationException("Invalid authorization request.");
        }

        var domainHint = context.Parameters.AllKeys.Contains("domain_hint")
            ? context.Parameters["domain_hint"]
            : null;
        if (!string.IsNullOrWhiteSpace(domainHint))
        {
            // Unchanged org-SSO path.
            return RedirectToAction("Login", "Sso", new { returnUrl });
        }

        if (IsPartnerRequest(context))
        {
            return Redirect(BuildVaultAuthorizeUrl(returnUrl));
        }

        throw new InvalidOperationException("No domain_hint provided");
    }

    /// <summary>
    /// Consent dispatcher (IdentityServer <c>UserInteraction.ConsentUrl</c>). Only partner clients require
    /// consent; they are sent to the same web vault consent page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Consent(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context != null && IsPartnerRequest(context))
        {
            return Redirect(BuildVaultAuthorizeUrl(returnUrl));
        }

        throw new InvalidOperationException("Consent is not supported for this client.");
    }

    /// <summary>
    /// Returns the display metadata the web vault needs to render the consent screen for a pending partner
    /// authorize request. Public information about the request only — no user data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> AuthorizeContext(string returnUrl)
    {
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.Client == null || !_partnerStore.IsRegistered(context.Client.ClientId))
        {
            return NotFound();
        }

        var partner = _partnerStore.GetByClientId(context.Client.ClientId);
        return Json(new
        {
            clientId = context.Client.ClientId,
            clientName = partner?.Name ?? context.Client.ClientName,
            scopes = GetRequestedScopes(context),
            agentRedirectUri = partner == null ? null : GetAgentRedirectUri(context, partner),
        });
    }

    /// <summary>
    /// Completes the partner flow: validates the single-use consent token minted after the user approved in
    /// the web vault, signs the user in, grants consent, and resumes the original authorize request (which
    /// then issues the code to the registered redirect URI). The PSK is never involved here.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ConsentCallback(string token, string returnUrl)
    {
        if (!_featureService.IsEnabled(FeatureFlagKeys.OpenIdConnectProvider))
        {
            return NotFound();
        }

        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        if (context?.Client == null || !_partnerStore.IsRegistered(context.Client.ClientId))
        {
            throw new InvalidOperationException("Invalid authorization request.");
        }

        if (!_consentTokenFactory.TryUnprotect(token, out var consentToken) || consentToken == null ||
            !consentToken.IsValidFor(consentToken.UserId, returnUrl))
        {
            throw new InvalidOperationException("Invalid consent token.");
        }

        // Single-use: a replayed callback fails here even with an otherwise-valid token.
        if (!await _nonceStore.TryConsumeAsync(consentToken.Nonce))
        {
            throw new InvalidOperationException("Consent token has already been used or has expired.");
        }

        await HttpContext.SignInAsync(new IdentityServerUser(consentToken.UserId.ToString())
        {
            IdentityProvider = IdentityServerConstants.LocalIdentityProvider,
        });

        await _interaction.GrantConsentAsync(context, new ConsentResponse
        {
            ScopesValuesConsented = GetRequestedScopes(context),
            RememberConsent = true,
        }, consentToken.UserId.ToString());

        return Redirect(returnUrl);
    }

    private bool IsPartnerRequest(AuthorizationRequest context) =>
        _featureService.IsEnabled(FeatureFlagKeys.OpenIdConnectProvider) &&
        context.Client?.ClientId != null &&
        _partnerStore.IsRegistered(context.Client.ClientId);

    private string BuildVaultAuthorizeUrl(string returnUrl)
    {
        var vaultUri = _globalSettings.BaseServiceUri.Vault;
        return $"{vaultUri}/#/oauth/authorize?returnUrl={Uri.EscapeDataString(returnUrl)}";
    }

    /// <summary>
    /// The agent's loopback redirect the web vault completion connector forwards the PSK to. Supplied by
    /// the agent as an <c>agent_redirect_uri</c> parameter on the original authorize request (distinct from
    /// the OAuth <c>redirect_uri</c>, which is the web vault connector), and validated against the
    /// partner's registered <see cref="OpenIdConnectPartner.AgentRedirectUris"/> allow-list so the PSK is
    /// only ever delivered to an operator-vetted destination — never a free-form URL from the request.
    /// </summary>
    private static string? GetAgentRedirectUri(AuthorizationRequest context, OpenIdConnectPartner partner)
    {
        var requested = context.Parameters.AllKeys.Contains("agent_redirect_uri")
            ? context.Parameters["agent_redirect_uri"]
            : null;
        if (string.IsNullOrWhiteSpace(requested) ||
            !Uri.TryCreate(requested, UriKind.Absolute, out var requestedUri))
        {
            return null;
        }

        foreach (var allowed in partner.AgentRedirectUris)
        {
            if (Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri) &&
                IsAllowedAgentRedirect(allowedUri, requestedUri))
            {
                return requested;
            }
        }

        return null;
    }

    /// <summary>
    /// A requested agent redirect matches a registered one when scheme, host, and path match. For loopback
    /// hosts the port is ignored (RFC 8252 — native apps use ephemeral loopback ports); otherwise the port
    /// must match exactly.
    /// </summary>
    private static bool IsAllowedAgentRedirect(Uri allowed, Uri requested)
    {
        var schemeHostPathMatch =
            string.Equals(allowed.Scheme, requested.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(allowed.Host, requested.Host, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(allowed.AbsolutePath, requested.AbsolutePath, StringComparison.Ordinal);

        if (!schemeHostPathMatch)
        {
            return false;
        }

        return allowed.IsLoopback || allowed.Port == requested.Port;
    }

    private static IEnumerable<string> GetRequestedScopes(AuthorizationRequest context)
    {
        var rawScopes = context.ValidatedResources?.RawScopeValues;
        if (rawScopes != null && rawScopes.Any())
        {
            return rawScopes;
        }

        var scopeParam = context.Parameters.AllKeys.Contains("scope") ? context.Parameters["scope"] : null;
        return string.IsNullOrWhiteSpace(scopeParam)
            ? []
            : scopeParam.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
