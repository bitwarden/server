using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface ISubmitAccessRequestCommand
{
    /// <summary>
    /// Submits a request to lease a cipher. On the automatic path a lease is issued immediately; on the human path a
    /// pending request is created. The submission's shape is validated against the cipher's resolved approval outcome.
    /// </summary>
    /// <returns>
    /// The submitted request, or the failure that stopped it: <see cref="Errors.AccessRequestNotFound"/> when the
    /// cipher is not the caller's to see, one of the three "you already have this" conflicts
    /// (<see cref="Errors.AccessAlreadyActive"/>, <see cref="Errors.AccessRequestAlreadyPending"/>,
    /// <see cref="Errors.AccessRequestAlreadyApproved"/>), a shape or bounds failure against the governing rule, or
    /// a denial by that rule's conditions.
    /// </returns>
    Task<CommandResult<AccessRequestResult>> SubmitAsync(Guid userId, Guid cipherId, AccessRequestSubmission submission);
}
