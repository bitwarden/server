using Bit.Core.Settings;
using IdentityServer4;
using IdentityServer4.Models;

namespace Bit.Core.IdentityServer
{
    public class OidcIdentityClient : Client
    {
        public OidcIdentityClient(GlobalSettings globalSettings)
        {
            ClientId = "oidc-identity";
            RequireClientSecret = true;
            RequirePkce = true;
            ClientSecrets = new List<Secret> { new Secret(globalSettings.OidcIdentityClientKey.Sha256()) };
            AllowedScopes = new string[]
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile
            };
            AllowedGrantTypes = GrantTypes.Code;
            Enabled = true;
            RedirectUris = new List<string> { $"{globalSettings.BaseServiceUri.Identity}/signin-oidc" };
            RequireConsent = false;
        }
    }
}
