using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ICancelAccessRequestCommand
{
    /// <summary>
    /// Withdraws an access request that has not produced a lease: the requester withdrawing their own, or a managing
    /// approver retracting it. Drops it from any approver's inbox.
    /// </summary>
    /// <returns>
    /// Nothing on success, or one of <see cref="Errors.AccessRequestNotFound"/> (no such request, or the caller is
    /// neither its requester nor a managing approver), <see cref="Errors.AccessRequestAlreadyResolved"/> or
    /// <see cref="Errors.AccessRequestHasActiveLease"/>.
    /// </returns>
    Task<CommandResult> CancelAsync(Guid userId, Guid requestId);
}
