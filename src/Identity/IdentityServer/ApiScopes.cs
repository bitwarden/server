using IdentityServer4.Models;
using static Bit.Core.Utilities.IdentityConstants;

namespace Bit.Identity.IdentityServer;

public static class ApiScopes
{
    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            new ApiScope(Scopes.Api, "API Access"),
            new ApiScope(Scopes.ApiPush, "API Push Access"),
            new ApiScope(Scopes.ApiLicensing, "API Licensing Access"),
            new ApiScope(Scopes.ApiOrganization, "API Organization Access"),
            new ApiScope(Scopes.ApiInstallation, "API Installation Access"),
            new ApiScope(Scopes.Internal, "Internal Access"),
        };
    }
}
