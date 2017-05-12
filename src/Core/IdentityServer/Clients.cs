using IdentityServer4.Models;
using System.Collections.Generic;

namespace Bit.Core.IdentityServer
{
    public class Clients
    {
        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new ApiClient("mobile", 90, 1),
                new ApiClient("web", 1, 1),
                new ApiClient("browser", 30, 1),
                new ApiClient("desktop", 30, 1),
                new ApiClient("connector", 30, 24)
            };
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
                RequireClientSecret = false;
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword;
                RefreshTokenExpiration = TokenExpiration.Sliding;
                RefreshTokenUsage = TokenUsage.ReUse;
                SlidingRefreshTokenLifetime = 86400 * refreshTokenSlidingDays;
                AbsoluteRefreshTokenLifetime = int.MaxValue; // forever
                UpdateAccessTokenClaimsOnRefresh = true;
                AccessTokenLifetime = 3600 * accessTokenLifetimeHours;
                AllowOfflineAccess = true;

                if(scopes == null)
                {
                    scopes = new string[] { "api" };
                }
                AllowedScopes = scopes;
            }
        }
    }
}
