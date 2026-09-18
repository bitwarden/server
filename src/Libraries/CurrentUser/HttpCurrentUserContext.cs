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

    public Guid UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User
                ?? throw new InvalidOperationException("No HTTP request is in scope.");

            return Guid.TryParse(principal.FindFirstValue(_userIdClaimType), out var userId)
                ? userId
                : throw new InvalidOperationException("The current principal does not contain a valid user id claim.");
        }
    }
}
