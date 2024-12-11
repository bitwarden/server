using Bit.Core.Identity;
using Bit.Core.IdentityServer;
using Duende.IdentityServer.Models;
using IdentityModel;

namespace Bit.Identity.IdentityServer;

public class ApiResources
{
    public static IEnumerable<ApiResource> GetApiResources()
    {
        return new List<ApiResource>
        {
            new(
                "api",
                new[]
                {
                    JwtClaimTypes.Name,
                    JwtClaimTypes.Email,
                    JwtClaimTypes.EmailVerified,
                    Claims.SecurityStamp,
                    Claims.Premium,
                    Claims.Device,
                    Claims.OrganizationOwner,
                    Claims.OrganizationAdmin,
                    Claims.OrganizationUser,
                    Claims.OrganizationCustom,
                    Claims.ProviderAdmin,
                    Claims.ProviderServiceUser,
                    Claims.SecretsManagerAccess,
                }
            ),
            new(ApiScopes.Internal, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiPush, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiLicensing, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiOrganization, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiInstallation, new[] { JwtClaimTypes.Subject }),
            new(ApiScopes.ApiSecrets, new[] { JwtClaimTypes.Subject, Claims.Organization }),
        };
    }
}
