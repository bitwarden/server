using Bit.Core.Context;
using Bit.Core.Exceptions;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The per-seat PAM license check (<c>OrganizationUser.AccessPam</c>) on submit, activate and extend; revoke, cancel
/// and reads stay open. Reads the token claim, so a new license applies from the next token refresh.
/// </summary>
public static class PamLicenseGuard
{
    /// <summary>Says nothing about the rule, the collection, or who may approve.</summary>
    public const string UnlicensedMessage =
        "A Privileged Controls license is required to access this item. Ask your admin to activate your license.";

    public static void RequireLicense(this ICurrentContext currentContext, Guid organizationId)
    {
        if (!currentContext.AccessPam(organizationId))
        {
            throw new BadRequestException(UnlicensedMessage);
        }
    }
}
