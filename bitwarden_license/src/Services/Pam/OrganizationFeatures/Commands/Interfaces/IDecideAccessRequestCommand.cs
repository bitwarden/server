using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Models;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDecideAccessRequestCommand
{
    /// <summary>
    /// Approves or denies a pending lease request on behalf of an approver. The caller must be able to Manage the
    /// request's collection and must not be the requester. An approval does not mint the lease — the requester
    /// activates the approved request when they access the item. Returns the updated inbox row.
    /// </summary>
    /// <returns>
    /// The updated row, or one of <see cref="Errors.AccessRequestNotFound"/> (no such request, or the caller cannot
    /// Manage its collection), <see cref="Errors.AccessRequestAlreadyResolved"/>,
    /// <see cref="Errors.CannotDecideOwnRequest"/> or <see cref="Errors.RequestedWindowEnded"/>.
    /// </returns>
    Task<CommandResult<AccessRequestDetails>> DecideAsync(Guid userId, Guid requestId, AccessDecisionSubmission submission);
}
