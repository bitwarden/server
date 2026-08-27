using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Activates the caller's approved access request: mints the active lease that authorizes access, spanning the
    /// request's approved window. Only the requester may activate, and only while the window is open. Activation is
    /// idempotent while the produced lease is live — a repeat call returns the existing lease.
    ///
    /// The automated conditions of the rule the request pinned at submit are re-evaluated here, against the caller's
    /// signals at activation, and a lease is minted only if they still admit them. This is the last gate: once a lease
    /// exists it authorizes access for its whole window on its own existence, so an approval obtained under conditions
    /// that no longer hold must not be spendable.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is not approved (still pending, or denied/cancelled/expired), or it already produced a lease that
    /// has since been revoked or has lapsed — a request authorizes access at most once.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The approved window has not started yet or has already ended, or the governing rule's automated conditions no
    /// longer admit the caller (their source IP has left the rule's allowlist, or the rule's stored conditions can no
    /// longer be read).
    /// </exception>
    /// <param name="now">The caller's clock. Every guard, the mint, and the audit trail use this one instant, and the
    /// caller must derive the response status against the same value — a second, later clock read could report a
    /// just-minted lease as already expired.</param>
    Task<AccessLease> ActivateAsync(Guid userId, Guid requestId, DateTime now);
}
