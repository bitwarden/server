using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRequestAccessCommand
{
    /// <summary>
    /// Submits a request to lease a cipher. On the automatic path a lease is issued immediately; on the human path a
    /// pending request is created. The submission's shape is validated against the cipher's resolved approval outcome.
    /// </summary>
    Task<AccessRequestResult> RequestAccessAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission);
}
