namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICancelAccessRequestCommand
{
    /// <summary>
    /// Revokes a request that has not been activated. The requester withdraws their own request, which becomes
    /// <see cref="Bit.Pam.Enums.AccessRequestStatus.Cancelled"/>; a managing approver retracts it, which records a
    /// Deny decision carrying <paramref name="reason"/>.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist, or the caller is neither its requester nor a managing approver.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The request is no longer pending or approved, has been activated, or its window has ended.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The request is an extension, or a managing approver revoked without a reason.
    /// </exception>
    Task CancelAsync(Guid userId, Guid requestId, string? reason);
}
