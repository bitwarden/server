using Bit.Pam.Models;
using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IDecideAccessRequestCommand
{
    /// <summary>
    /// Approves or denies a pending request; an approval does not mint the lease. The requester cannot decide their
    /// own request.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The request does not exist or the caller cannot Manage its collection.
    /// </exception>
    Task<AccessRequestDetails> DecideAsync(Guid userId, Guid requestId, AccessDecisionSubmission submission);
}
