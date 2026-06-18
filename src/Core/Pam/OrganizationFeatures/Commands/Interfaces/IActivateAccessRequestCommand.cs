using Bit.Pam.Entities;

namespace Bit.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Activates the caller's approved access request: mints the active lease that authorizes access, spanning the
    /// request's approved window. Only the requester may activate, and only while the window is open. Activation is
    /// idempotent while the produced lease is live — a repeat call returns the existing lease.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is not approved (still pending, or denied/cancelled/expired), or it already produced a lease that
    /// has since been revoked or has lapsed — a request authorizes access at most once.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The approved window has not started yet or has already ended.
    /// </exception>
    Task<AccessLease> ActivateAsync(Guid userId, Guid requestId);
}
