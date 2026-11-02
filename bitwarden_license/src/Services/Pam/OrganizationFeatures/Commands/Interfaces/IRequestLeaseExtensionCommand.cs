using Bit.Pam.Models;
using Bit.Services.Pam.Models;
namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Pushes the caller's lease end out in place, auto-approved under the rule the lease was granted under.
    /// </summary>
    /// <remarks>
    /// A lease that ends before the extension applies resolves the request Denied rather than throwing.
    /// </remarks>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The item is not lease-gated, its rule is inactive or disallows extensions, the duration is out of range, no
    /// justification was supplied, the lease was already extended, or the rule's automated conditions deny the caller.
    /// </exception>
    Task<AccessRequestDetails> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission);
}
