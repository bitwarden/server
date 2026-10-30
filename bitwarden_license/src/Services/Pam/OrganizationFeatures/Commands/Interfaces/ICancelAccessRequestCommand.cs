namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICancelAccessRequestCommand
{
    /// <summary>
    /// Revokes an unactivated request. The requester's withdrawal cancels it; a managing approver's retraction denies
    /// it and requires <paramref name="reason"/>.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist, or the caller is neither its requester nor a managing approver.
    /// </exception>
    Task CancelAsync(Guid userId, Guid requestId, string? reason);
}
