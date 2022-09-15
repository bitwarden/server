using IdentityModel;
using IdentityServer4.Models;

namespace Bit.Identity.IdentityServer;

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
                "orguser",
                "orgcustom",
                "providerprovideradmin",
                "providerserviceuser",
            }),
            new ApiResource("internal", new string[] { JwtClaimTypes.Subject }),
            new ApiResource("api.push", new string[] { JwtClaimTypes.Subject }),
            new ApiResource("api.licensing", new string[] { JwtClaimTypes.Subject }),
            new ApiResource("api.organization", new string[] { JwtClaimTypes.Subject }),
            new ApiResource("api.provider", new string[] { JwtClaimTypes.Subject }),
            new ApiResource("api.installation", new string[] { JwtClaimTypes.Subject }),
        };
    }
}
