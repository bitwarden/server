using IdentityServer4.Models;
using System.Collections.Generic;
using System.Security.Claims;

namespace Bit.Api.IdentityServer
{
    public class ApiResources
    {
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("api", new string[] {
                    ClaimTypes.AuthenticationMethod,
                    ClaimTypes.NameIdentifier,
                    ClaimTypes.Email,
                    "securitystamp",

                    "name",
                    "email",
                    "sstamp", // security stamp
                    "plan",
                    "device",
                    "orgowner",
                    "orgadmin",
                    "orguser"
                })
            };
        }
    }
}
