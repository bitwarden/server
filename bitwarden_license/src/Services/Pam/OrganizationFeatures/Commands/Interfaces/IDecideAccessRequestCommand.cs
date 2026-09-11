using Bit.Pam.Models;
using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDecideAccessRequestCommand
{
    /// <summary>
    /// Approves or denies a pending lease request on behalf of an approver. The caller must be able to Manage the
    /// request's collection and must not be the requester. An approval does not mint the lease. Returns the
    /// updated inbox row.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller cannot Manage its collection.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">The request is no longer pending.</exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// Self-approval, or an approval verdict on a window that has already ended. 400 rather than 403 for
    /// self-approval: Bitwarden clients treat 403 as a forced logout.
    /// </exception>
    Task<AccessRequestDetails> DecideAsync(Guid userId, Guid requestId, AccessDecisionSubmission submission);
}
