using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IActivateAccessRequestCommand
{
    /// <summary>
    /// Activates the caller's approved access request: mints the active lease that authorizes access, spanning the
    /// request's approved window. Only the requester may activate, and only while the window is open. Activation is
    /// idempotent while the produced lease is live — a repeat call returns the existing lease.
    /// </summary>
    /// <returns>
    /// The lease, or one of <see cref="Errors.AccessRequestNotFound"/> (no such request, or not the caller's),
    /// <see cref="Errors.AccessLeaseAlreadyUsed"/>, <see cref="Errors.AccessRequestNotApproved"/>,
    /// <see cref="Errors.AccessRequestNotActivatable"/>, <see cref="Errors.ApprovedWindowNotStarted"/>,
    /// <see cref="Errors.ApprovedWindowEnded"/> or <see cref="Errors.SingleActiveLeaseConflict"/>.
    /// </returns>
    Task<CommandResult<AccessLease>> ActivateAsync(Guid userId, Guid requestId);
}
