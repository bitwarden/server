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
                    PostLogoutRedirectUris = new[] { "bitwarden-desktop://logged-out" };
                }
                else if (id == "connector")
                {
                    RedirectUris = new[] { "bwdc://sso-callback" };
                    PostLogoutRedirectUris = new[] { "bwdc://logged-out" };
                }
                else if (id == "browser")
                {
                    // TODO
                }
                else if (id == "cli")
                {
                    // TODO
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
    }
}
