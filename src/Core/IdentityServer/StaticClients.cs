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
                    var connectorUris = new List<string>();
                    for (var port = 8065; port <= 8070; port++)
                    {
                        connectorUris.Add(string.Format("http://localhost:{0}", port));
                    }
                    RedirectUris = connectorUris.Append("bwdc://sso-callback").ToList();
                    PostLogoutRedirectUris = connectorUris.Append("bwdc://logged-out").ToList();
                }
                else if (id == "browser")
                {
                    RedirectUris = new[] { "https://localhost:8080/sso-connector.html" };
                    PostLogoutRedirectUris = new[] { "https://localhost:8080" };
                    AllowedCorsOrigins = new[] { "https://localhost:8080" };
                }
                else if (id == "cli")
                {
                    var cliUris = new List<string>();
                    for (var port = 8065; port <= 8070; port++)
                    {
                        cliUris.Add(string.Format("http://localhost:{0}", port));
                    }
                    RedirectUris = cliUris;
                    PostLogoutRedirectUris = cliUris;
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
