using IdentityModel;
using IdentityServer4.Models;
using System.Collections.Generic;

namespace Bit.Core.IdentityServer
{
    public class ApiResources
    {
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("api", new string[] {
                    JwtClaimTypes.Name,
                    JwtClaimTypes.Email,
                    JwtClaimTypes.EmailVerified,
                    "sstamp", // security stamp
                    "premium",
                    "device",
                    "orgowner",
                    "orgadmin",
                    "orguser"
                }),
                new ApiResource("api.push")
            };
        }
    }
}
