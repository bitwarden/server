using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Models;
using Bit.Services.Pam.Models;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRequestLeaseExtensionCommand
{
    /// <summary>
    /// Extends the caller's active lease by the requested duration. Extensions are always auto-approved, subject to
    /// the governing rule's <c>AllowsExtensions</c> / <c>MaxExtensionDurationSeconds</c> settings: the lease's end is
    /// pushed out in place (no new lease is minted) and an auto-approved extension request is recorded. Only the
    /// lease's requester may extend it.
    /// </summary>
    /// <returns>
    /// The recorded extension, or one of <see cref="Errors.AccessLeaseNotFound"/> (no such lease, or not the
    /// caller's), <see cref="Errors.AccessLeaseNoLongerActive"/>, <see cref="Errors.CipherNotGated"/>,
    /// <see cref="Errors.ExtensionsNotAllowed"/>, <see cref="Errors.DurationMustBePositive"/>,
    /// <see cref="Errors.ExtensionExceedsMax"/>, <see cref="Errors.ExtensionReasonRequired"/> or
    /// <see cref="Errors.AccessLeaseAlreadyExtended"/>.
    /// </returns>
    Task<CommandResult<AccessRequestDetails>> ExtendAsync(Guid userId, AccessLeaseExtensionSubmission submission);
}
