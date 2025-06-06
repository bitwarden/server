using System.Security.Claims;

namespace Bit.Core.Utilities;

#nullable enable

public static class ClaimsExtensions
{
    public static bool HasSsoIdP(this IEnumerable<Claim> claims)
    {
        return claims.Any(c => c.Type == "idp" && c.Value == "sso");
    }
}
