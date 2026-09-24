using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ISubmitAccessRequestCommand
{
    /// <summary>
    /// Submits a request to lease a cipher: approved at once on the automatic path, pending an approver on the
    /// human path. Neither path mints a lease.
    /// </summary>
    Task<AccessRequestResult> SubmitAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission);
}
