namespace Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRevokeAccessLeaseCommand
{
    /// <summary>
    /// Ends an active lease early, settling it to revoked. The caller must be either the lease's holder (ending their
    /// own access) or able to Manage the lease's collection (a managing approver or org admin); the actor is recorded
    /// as the revoker. The optional reason is retained for the audit trail.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist, or the caller is neither its holder nor able to Manage its collection.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">The lease is not active.</exception>
    Task RevokeAsync(Guid userId, Guid leaseId, string? reason);
}
