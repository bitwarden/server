using System.Security.Claims;
using Bit.Core.Auth.Identity;

namespace Bit.Core.Auth.UserFeatures.SendAccess;

public static class SendAccessClaimsPrincipalExtensions
{
    public static Guid GetSendId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var sendIdClaim = user.FindFirst(Claims.SendAccessClaims.SendId)
            ?? throw new InvalidOperationException("send_id claim not found.");

        if (!Guid.TryParse(sendIdClaim.Value, out var sendGuid))
        {
            throw new InvalidOperationException("Invalid send_id claim value.");
        }

        return sendGuid;
    }
}
