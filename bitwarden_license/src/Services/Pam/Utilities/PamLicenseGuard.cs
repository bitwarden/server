using Bit.Core.Context;
using Bit.Core.Exceptions;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The per-seat PAM license check (<c>OrganizationUser.AccessPam</c>) on the leasing paths that acquire access.
/// </summary>
/// <remarks>
/// Guards submit, activate and extend only; revoke, cancel and reads stay open. Reads the token claim, so a
/// new license applies from the next token refresh.
/// </remarks>
public static class PamLicenseGuard
{
    /// <summary>
    /// The refusal message. It says nothing about the rule, the collection, or who may approve.
    /// </summary>
    public const string UnlicensedMessage =
        "A Privileged Controls license is required to access this item. Ask your admin to activate your license.";

    /// <summary>
    /// Throws <see cref="BadRequestException"/> with <see cref="UnlicensedMessage"/> if the caller holds no PAM
    /// license in <paramref name="organizationId"/>.
    /// </summary>
    public static void RequireLicense(this ICurrentContext currentContext, Guid organizationId)
    {
        if (!currentContext.AccessPam(organizationId))
        {
            throw new BadRequestException(UnlicensedMessage);
        }
    }
}
