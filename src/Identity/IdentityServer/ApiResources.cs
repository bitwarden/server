using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using IdentityModel;
using IdentityServer4.Models;

namespace Bit.Identity.IdentityServer;

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
            new(ApiScopes.Internal, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiPush, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiLicensing, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiOrganization, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiInstallation, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiSecrets, new[] { JwtClaimTypes.Subject, Claims.Organization }),
        };
    }
}
