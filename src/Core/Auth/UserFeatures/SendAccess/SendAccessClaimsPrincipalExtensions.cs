using System.Security.Claims;
using Bit.Core.Identity;

namespace Bit.Core.Auth.UserFeatures.SendAccess;

// TODO: test this with test file in Tests/Core/Auth/UserFeatures/SendAccess/SendAccessClaimsPrincipalExtensionsTests.cs
public static class SendAccessClaimsPrincipalExtensions
{
    public static Guid GetSendId(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var sendIdClaim = user.FindFirst(Claims.SendId);
        if (sendIdClaim == null) throw new InvalidOperationException("Send ID claim not found.");

        if (!Guid.TryParse(sendIdClaim.Value, out var sendGuid))
        {
            throw new InvalidOperationException("Invalid Send ID claim value.");
        }

        return sendGuid;
    }
}
