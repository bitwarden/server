using Bit.Core.Settings;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Bit.Sso.IdentityServer;

public class OidcIdentityClient : Client
{
    public OidcIdentityClient(GlobalSettings globalSettings)
    {
        ClientId = "oidc-identity";
        RequireClientSecret = true;
        RequirePkce = true;
        ClientSecrets = new List<Secret> { new(globalSettings.OidcIdentityClientKey.Sha256()) };
        AllowedScopes = new[]
        {
            IdentityServerConstants.StandardScopes.OpenId,
            IdentityServerConstants.StandardScopes.Profile,
        };
        AllowedGrantTypes = GrantTypes.Code;
        Enabled = true;
        RedirectUris = new List<string> { $"{globalSettings.BaseServiceUri.Identity}/signin-oidc" };
        RequireConsent = false;
    }
}
