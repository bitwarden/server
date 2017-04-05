using IdentityServer4.Models;
using System.Collections.Generic;

namespace Bit.Api.IdentityServer
{
    public class Clients
    {
        public static IEnumerable<Client> GetClients()
        {
            return new List<Client>
            {
                new ApiClient("mobile"),
                new ApiClient("web"),
                new ApiClient("browser"),
                new ApiClient("desktop")
            };
        }

        public class ApiClient : Client
        {
            public ApiClient(string id, string[] additionalScopes = null)
            {
                ClientId = id;
                RequireClientSecret = false;
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword;
                UpdateAccessTokenClaimsOnRefresh = true;
                AccessTokenLifetime = 60 * 60; // 1 hour
                AllowOfflineAccess = true;

                var scopes = new List<string> { "api" };
                if(additionalScopes != null)
                {
                    scopes.AddRange(additionalScopes);
                }
                AllowedScopes = scopes;
            }
        }
    }
}
