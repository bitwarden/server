using IdentityServer4.Models;
using System.Collections.Generic;

namespace Bit.Core.IdentityServer
{
    public class ApiScopes
    {
        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new ApiScope("api", "API Access"),
                new ApiScope("api.push", "API Push Access"),
                new ApiScope("api.licensing", "API Licensing Access"),
                new ApiScope("api.organization", "API Organization Access"),
                new ApiScope("internal", "Internal Access")
            };
        }
    }
}
