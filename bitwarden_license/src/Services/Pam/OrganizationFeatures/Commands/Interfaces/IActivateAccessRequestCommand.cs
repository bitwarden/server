using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Activates the caller's approved access request: mints the active lease that authorizes access, spanning
    /// the request's approved window. Only the requester may activate, only while the window is open.
    /// Idempotent while the produced lease is live.
    /// </summary>
    /// <remarks>
    /// The rule's automated conditions, pinned at submit, are re-evaluated against the caller's signals at
    /// activation, and a lease is minted only if they still admit them — this is the last gate, since once a
    /// lease exists it authorizes access for its whole window on its own existence.
    /// </remarks>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is not approved, or it already produced a lease that has since been revoked or lapsed.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The approved window has not started or has already ended, or the governing rule's automated
    /// conditions no longer admit the caller.
    /// </exception>
    /// <param name="now">The caller's clock. Every guard, the mint, and the audit trail use this one instant.</param>
    Task<AccessLease> ActivateAsync(Guid userId, Guid requestId, DateTime now);
}
