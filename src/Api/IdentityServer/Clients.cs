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
            public ApiClient(string id)
            {
                ClientId = id;
                RequireClientSecret = false;
                AllowedGrantTypes = GrantTypes.ResourceOwnerPassword;
                AllowOfflineAccess = true;
                AllowedScopes = new string[] { "api" };
            }
        }
    }
}
