using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Bit.CurrentUser;

internal sealed class HttpCurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    /// <remarks>
    /// Bitwarden tokens carry the user id in the OIDC "sub" claim, and JWT bearer runs with
    /// MapInboundClaims = false, so the raw claim type reaches the principal unrenamed.
    /// </remarks>
    private const string _userIdClaimType = "sub";

    public Guid UserId =>
        TryGetUserId(out var userId)
            ? userId
            : throw new InvalidOperationException(
                "No HTTP request is in scope, or the current principal does not contain a valid user id claim.");

    public bool TryGetUserId(out Guid userId) =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(_userIdClaimType), out userId);
}
