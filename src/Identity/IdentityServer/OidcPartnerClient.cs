using Bit.Core.Auth.IdentityServer;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Bit.Identity.IdentityServer;

/// <summary>
/// A pre-registered third-party "Login with Bitwarden" OIDC client. Authorization-code + PKCE with a
/// client secret and mandatory consent, restricted to identity scopes (openid/profile/email). It
/// deliberately cannot request <c>api</c> scope, cannot use offline access (no refresh token), so a token
/// issued to a partner yields the user's identity only and never reaches the vault.
/// </summary>
public class OidcPartnerClient : Client
{
    /// <summary>One-shot access token lifetime; just long enough for a single userinfo read.</summary>
    public const int AccessTokenLifetimeSeconds = 300;

    public OidcPartnerClient(OpenIdConnectPartner partner)
    {
        ClientId = partner.ClientId;
        ClientName = partner.Name;

        AllowedGrantTypes = GrantTypes.Code;
        RequirePkce = true;
        RequireClientSecret = true;
        RequireConsent = true;
        AllowOfflineAccess = false;

        ClientSecrets = new[] { new Secret(partner.ClientSecretHash) };
        RedirectUris = partner.RedirectUris.ToList();

        AllowedScopes = new[]
        {
            IdentityServerConstants.StandardScopes.OpenId,
            IdentityServerConstants.StandardScopes.Profile,
            IdentityServerConstants.StandardScopes.Email,
        };

        AccessTokenLifetime = AccessTokenLifetimeSeconds;
        UpdateAccessTokenClaimsOnRefresh = false;
    }
}
