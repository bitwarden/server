using Bit.Pam.Models;

namespace Bit.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Extends the caller's active lease by the requested duration. Extensions are always auto-approved, subject to
    /// the governing rule's <c>AllowsExtensions</c> / <c>MaxExtensionDurationSeconds</c> settings: the lease's end is
    /// pushed out in place (no new lease is minted) and an auto-approved extension request is recorded. Only the
    /// lease's requester may extend it.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist or the caller is not its requester.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">
    /// The lease is no longer active (revoked or expired).
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.BadRequestException">
    /// The item is not lease-gated or does not allow extensions, the lease has already been extended, the duration is
    /// non-positive or exceeds the maximum extension length, or no justification was supplied.
    /// </exception>
    Task<AccessRequestDetails> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission);
}
