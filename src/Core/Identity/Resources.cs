using IdentityServer4.Models;
using System.Collections.Generic;

namespace Bit.Core.Identity
{
    public class Resources
    {
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new ApiResource("api", "Vault API", new string[] {
                    "authmethod",
                    "nameid",
                    "email",
                    "securitystamp"
                })
            };
        }
    }
}
