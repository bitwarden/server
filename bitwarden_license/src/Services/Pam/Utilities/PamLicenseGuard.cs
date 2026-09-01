using Bit.Core.Context;
using Bit.Core.Exceptions;

namespace Bit.Services.Pam.Utilities;

/// <summary>
/// The per-seat license check on the leasing paths that ACQUIRE access. A member of an organization subscribed to
/// PAM still needs a license of their own (<c>OrganizationUser.AccessPam</c>) before they may hold a credential;
/// without one the v0 billing model fails the attempt and refers them to their admin.
/// </summary>
/// <remarks>
/// Guards the acquiring paths only — submit, activate, extend. The terminating paths (revoke a lease, cancel a
/// request) stay open deliberately, so a member whose license is withdrawn while they hold a live lease can still
/// give it back; refusing there would strand access that the licensing change was meant to remove.
///
/// The read paths (pre-check, per-cipher access state) stay open too. They carry no credential — the cipher's
/// secrets are withheld by <c>ICipherLeaseGate</c> on the absence of a lease, licensed or not — and the client needs
/// the access state to recognise the cipher as gated at all, which is what lets it explain the licensing block
/// instead of rendering an empty item.
///
/// Reads the claim rather than the row: <see cref="ICurrentContext.AccessPam"/> resolves from the token, so a
/// license granted mid-session takes effect on the next token refresh. That is the same latency
/// <c>AccessSecretsManager</c> has carried since Secrets Manager shipped, and the client's own copy of the flag
/// comes from sync, which refreshes sooner — so the member sees the block lift before the server would let them act
/// on it either way.
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
    /// Throws <see cref="BadRequestException"/> with <see cref="UnlicensedMessage"/> when the caller holds no PAM
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
