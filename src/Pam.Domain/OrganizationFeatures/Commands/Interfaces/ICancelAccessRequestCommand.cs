namespace Bit.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICancelAccessRequestCommand
{
    /// <summary>
    /// Withdraws the caller's own pending access request: transitions it to
    /// <see cref="Enums.AccessRequestStatus.Cancelled"/> and drops it from any approver's inbox. Only the requester
    /// may cancel, and only while the request is still pending.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is no longer pending (already approved, denied, cancelled, or expired) and cannot be withdrawn.
    /// </exception>
    Task CancelAsync(Guid userId, Guid requestId);
}
