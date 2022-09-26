using Bit.Core.Identity;
using IdentityModel;
using IdentityServer4.Models;

namespace Bit.Core.IdentityServer
{
    public class ApiResources
    {
        public static IEnumerable<ApiResource> GetApiResources()
        {
            return new List<ApiResource>
            {
                new("api", new[] {
                    JwtClaimTypes.Name,
                    JwtClaimTypes.Email,
                    JwtClaimTypes.EmailVerified,
                    Claims.SecurityStamp,
                    Claims.Premium,
                    Claims.Device,
                    Claims.OrganizationOwner,
                    Claims.OrganizationAdmin,
                    Claims.OrganizationManager,
                    Claims.OrganizationUser,
                    Claims.OrganizationCustom,
                    Claims.ProviderAdmin,
                    Claims.ProviderServiceUser,
                }),
                new("internal", new[] { JwtClaimTypes.Subject }),
                new("api.push", new[] { JwtClaimTypes.Subject }),
                new("api.licensing", new[] { JwtClaimTypes.Subject }),
                new("api.organization", new[] { JwtClaimTypes.Subject }),
                new("api.provider", new[] { JwtClaimTypes.Subject }),
                new("api.installation", new[] { JwtClaimTypes.Subject }),
                new("api.secrets", new[] { JwtClaimTypes.Subject }),
            };
        }
    }
}
