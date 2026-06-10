using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDecideAccessRequestCommand
{
    /// <summary>
    /// Approves or denies a pending lease request on behalf of an approver. The caller must be able to Manage the
    /// request's collection and must not be the requester. An approval does not mint the lease — the requester
    /// activates the approved request when they access the item. Returns the updated inbox row.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller cannot Manage its collection.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">The request is no longer pending.</exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The caller is the requester (self-approval), or the verdict is an approval but the requested window has
    /// already ended (the requester could never activate it). Self-approval per the spec calls for 403, but
    /// Bitwarden clients treat 403 as a forced logout, so this is surfaced as 400 — matching the existing convention
    /// in the Admin Console controllers.
    /// </exception>
    Task<AccessRequestDetails> DecideAsync(Guid userId, Guid requestId, AccessDecisionSubmission submission);
}
