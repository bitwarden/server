using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRevokeAccessLeaseCommand
{
    /// <summary>
    /// Ends an active lease early, settling it to revoked. The caller must be either the lease's holder (ending their
    /// own access) or able to Manage the lease's collection (a managing approver or org admin); the actor is recorded
    /// as the revoker. The optional reason is retained for the audit trail.
    /// </summary>
    /// <returns>
    /// Nothing on success, or one of <see cref="Errors.AccessLeaseNotFound"/> (no such lease, or the caller is
    /// neither its holder nor able to Manage its collection) or <see cref="Errors.AccessLeaseNotActiveForRevoke"/>.
    /// </returns>
    Task<CommandResult> RevokeAsync(Guid userId, Guid leaseId, string? reason);
}
