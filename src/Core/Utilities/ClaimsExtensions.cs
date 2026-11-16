using System.Security.Claims;
using Bit.Core.Auth.IdentityServer;

namespace Bit.Core.Utilities;

public static class ClaimsExtensions
{
    public static bool HasSsoIdP(this IEnumerable<Claim> claims)
    {
        return claims.Any(c => c.Type == "idp" && c.Value == AuthenticationSchemes.IdentitySsoOidc);
    }
}
