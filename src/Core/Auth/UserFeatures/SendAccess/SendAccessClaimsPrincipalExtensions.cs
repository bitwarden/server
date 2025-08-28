using System.Security.Claims;
using Bit.Core.Identity;

namespace Bit.Core.Auth.UserFeatures.SendAccess;

public static class SendAccessClaimsPrincipalExtensions
{
    public static Guid GetSendId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var sendIdClaim = user.FindFirst(Claims.SendId)
            ?? throw new InvalidOperationException("Send ID claim not found.");

        if (!Guid.TryParse(sendIdClaim.Value, out var sendGuid))
        {
            throw new InvalidOperationException("Invalid Send ID claim value.");
        }

        return sendGuid;
    }
}
