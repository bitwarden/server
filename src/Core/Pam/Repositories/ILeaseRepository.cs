using Bit.Core.Pam.Entities;

namespace Bit.Core.Pam.Repositories;

public interface ILeaseRepository
{
    Task<Lease?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns the caller's active lease for the cipher whose window contains <paramref name="now"/>, or null.
    /// </summary>
    Task<Lease?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Atomically creates an auto-approved <see cref="LeaseRequest"/>, its policy <see cref="LeaseDecision"/>, and an
    /// active <see cref="Lease"/> in a single transaction. The three entities must already have their ids assigned.
    /// This is the only way a <see cref="Lease"/> is created, so the request, decision, and lease never diverge.
    /// </summary>
    Task CreateAutoApprovedAsync(LeaseRequest request, LeaseDecision decision, Lease lease, DateTime now);

    /// <summary>
    /// Atomically revokes an active lease (setting its revoked date and revoker) and records the revocation reason as
    /// a human <paramref name="auditDecision"/> against the lease's originating request. The decision must already
    /// have its id assigned.
    /// </summary>
    Task RevokeAsync(Lease lease, LeaseDecision auditDecision, DateTime now);
}
