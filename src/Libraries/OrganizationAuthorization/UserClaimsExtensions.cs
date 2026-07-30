#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;

namespace Bit.Api.AdminConsole.Authorization;

public static class UserClaimsExtensions
{
    /// <summary>
    /// Parses the authenticated user's ID out of their claims, or returns null if the principal has no valid user ID
    /// claim (for example, an unauthenticated request or a machine token).
    /// </summary>
    /// <remarks>
    /// Reads the <c>sub</c> claim, which is what <c>AddCustomIdentityServices</c> configures as
    /// <c>ClaimsIdentityOptions.UserIdClaimType</c>. This intentionally avoids taking a dependency on
    /// <c>IUserService</c> (and therefore the whole Core service graph) just to read a claim.
    /// </remarks>
    public static Guid? GetUserId(this ClaimsPrincipal user)
        => Guid.TryParse(user.FindFirstValue(JwtClaimTypes.Subject), out var userId)
            ? userId
            : null;
}
