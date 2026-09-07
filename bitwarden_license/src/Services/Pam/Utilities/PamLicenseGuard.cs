using Bit.Core.Context;
using Bit.Core.Exceptions;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The per-seat license check on the leasing paths that acquire access. A member of a PAM-subscribed
/// organization still needs a license of their own (<c>OrganizationUser.AccessPam</c>) before holding a
/// credential.
/// </summary>
/// <remarks>
/// Guards the acquiring paths only — submit, activate, extend. The terminating paths (revoke, cancel) and the
/// read paths (pre-check, access state) stay open, so a de-licensed member can still give back a held lease,
/// and the client can still explain the licensing block instead of rendering an empty item.
///
/// Reads the claim rather than the row (<see cref="ICurrentContext.AccessPam"/> resolves from the token), so a
/// license granted mid-session takes effect on the next token refresh.
/// </remarks>
public static class PamLicenseGuard
{
    /// <summary>
    /// The refusal, as the client's error catalog spells it. Deliberately says nothing about the governing rule, the
    /// collection, or who may approve: an unlicensed caller is refused before any of that is consulted, and the copy
    /// must not become a channel for policy configuration.
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
