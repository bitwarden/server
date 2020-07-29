using IdentityServer4;
using IdentityServer4.Models;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.IdentityServer
{
    public class StaticClients
    {
        public static IDictionary<string, Client> GetApiClients()
        {
            return new List<Client>
            {
                new ApiClient("mobile", 90, 1),
                new ApiClient("web", 30, 1),
                new ApiClient("browser", 30, 1),
                new ApiClient("desktop", 30, 1),
                new ApiClient("cli", 30, 1),
                new ApiClient("connector", 30, 24)
            }.ToDictionary(c => c.ClientId);
        }

        public class ApiClient : Client
        {
            public ApiClient(
                string id,
                int refreshTokenSlidingDays,
                int accessTokenLifetimeHours,
                string[] scopes = null)
            {
                ClientId = id;
                AllowedGrantTypes = new[] { GrantType.ResourceOwnerPassword, GrantType.AuthorizationCode };
                RefreshTokenExpiration = TokenExpiration.Sliding;
                RefreshTokenUsage = TokenUsage.ReUse;
                SlidingRefreshTokenLifetime = 86400 * refreshTokenSlidingDays;
                AbsoluteRefreshTokenLifetime = 0; // forever
                UpdateAccessTokenClaimsOnRefresh = true;
                AccessTokenLifetime = 3600 * accessTokenLifetimeHours;
                AllowOfflineAccess = true;

                RequireConsent = false;
                RequirePkce = true;
                RequireClientSecret = false;
                if (id == "web")
                {
                    RedirectUris = new[] { "https://localhost:8080/sso-connector.html" };
                    PostLogoutRedirectUris = new[] { "https://localhost:8080" };
                    AllowedCorsOrigins = new[] { "https://localhost:8080" };
                }
                else if (id == "desktop")
                {
                    RedirectUris = new[] { "bitwarden://sso-callback" };
                    PostLogoutRedirectUris = new[] { "bitwarden://logged-out" };
                }
                else if (id == "connector")
                {
                    RedirectUris = new[] { "bwdc://sso-callback" };
                    PostLogoutRedirectUris = new[] { "bwdc://logged-out" };
                }
                else if (id == "browser")
                {
                    RedirectUris = new[] { "https://localhost:8080/sso-connector.html" };
                    PostLogoutRedirectUris = new[] { "https://localhost:8080" };
                    AllowedCorsOrigins = new[] { "https://localhost:8080" };
                }
                else if (id == "cli")
                {
                    RedirectUris = new[] { "bitwardencli://sso-callback" };
                    PostLogoutRedirectUris = new[] { "bitwardencli://logged-out" };
                }
                else if (id == "mobile")
                {
                    RedirectUris = new[] { "bitwarden://sso-callback" };
                    PostLogoutRedirectUris = new[] { "bitwarden://logged-out" };
                }

                if (scopes == null)
                {
                    scopes = new string[] { "api" };
                }
                AllowedScopes = scopes;
            }
        }

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
}
