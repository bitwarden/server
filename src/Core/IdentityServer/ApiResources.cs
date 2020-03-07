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
                    "orgmanager",
                    "orguser"
                }),
                new ApiResource("internal", new string[] { JwtClaimTypes.Subject }),
                new ApiResource("api.push", new string[] { JwtClaimTypes.Subject }),
                new ApiResource("api.licensing", new string[] { JwtClaimTypes.Subject }),
                new ApiResource("api.organization", new string[] { JwtClaimTypes.Subject })
            };
        }
    }
}
