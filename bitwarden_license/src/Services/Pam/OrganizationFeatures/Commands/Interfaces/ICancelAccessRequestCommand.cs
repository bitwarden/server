namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICancelAccessRequestCommand
{
    /// <summary>
    /// Withdraws the caller's own pending access request: transitions it to
    /// <see cref="Bit.Pam.Enums.AccessRequestStatus.Cancelled"/> and drops it from any approver's inbox.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is no longer pending (already approved, denied, cancelled, or expired) and cannot be withdrawn.
    /// </exception>
    Task CancelAsync(Guid userId, Guid requestId);
}
