using IdentityServer4.Models;

namespace Bit.Core.IdentityServer
{
    public class ApiScopes
    {
        public static IEnumerable<ApiScope> GetApiScopes()
        {
            return new List<ApiScope>
            {
                new("api", "API Access"),
                new("api.push", "API Push Access"),
                new("api.licensing", "API Licensing Access"),
                new("api.organization", "API Organization Access"),
                new("api.installation", "API Installation Access"),
                new("internal", "Internal Access"),
            };
        }
    }
}
