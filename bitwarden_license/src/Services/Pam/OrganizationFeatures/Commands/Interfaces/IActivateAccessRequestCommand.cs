using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Mints the lease for the caller's approved request, within its window, after re-checking the pinned rule's
    /// automated conditions. Idempotent while that lease is live.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    Task<AccessLease> ActivateAsync(Guid userId, Guid requestId, DateTime now);
}
