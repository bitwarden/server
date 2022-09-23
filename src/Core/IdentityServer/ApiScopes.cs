using IdentityServer4.Models;

namespace Bit.Core.IdentityServer;

public static class ApiScopes
{
    public const string Api = "api";
    public const string ApiPush = "api.push";
    public const string ApiLicensing = "api.licensing";
    public const string ApiOrganization = "api.organization";
    public const string ApiInstallation = "api.installation";
    public const string Internal = "internal";


    public static IEnumerable<ApiScope> GetApiScopes()
    {
        return new List<ApiScope>
        {
            new ApiScope(Api, "API Access"),
            new ApiScope(ApiPush, "API Push Access"),
            new ApiScope(ApiLicensing, "API Licensing Access"),
            new ApiScope(ApiOrganization, "API Organization Access"),
            new ApiScope(ApiInstallation, "API Installation Access"),
            new ApiScope(Internal, "Internal Access"),
        };
    }
}
