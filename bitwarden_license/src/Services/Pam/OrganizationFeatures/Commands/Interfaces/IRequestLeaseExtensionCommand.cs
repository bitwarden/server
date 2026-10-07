using Bit.Pam.Models;
using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Extends the caller's lease in place, approved automatically under the rule it was granted under. A lease that
    /// ends first resolves the request Denied rather than throwing.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist or the caller is not its requester.
    /// </exception>
    Task<AccessRequestDetails> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission);
}
