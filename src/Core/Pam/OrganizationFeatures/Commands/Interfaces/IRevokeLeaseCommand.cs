namespace Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;

public interface IRevokeLeaseCommand
{
    /// <summary>
    /// Revokes an active lease early. The caller must be able to Manage the lease's collection. The optional reason is
    /// retained for the audit trail.
    /// </summary>
    /// <exception cref="Bit.Core.Exceptions.NotFoundException">
    /// The lease does not exist or the caller cannot Manage its collection.
    /// </exception>
    /// <exception cref="Bit.Core.Exceptions.ConflictException">The lease is not active.</exception>
    Task RevokeAsync(Guid userId, Guid leaseId, string? reason);
}
