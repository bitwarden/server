using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Repositories;

public interface IAccessLeaseRepository
{
    Task<AccessLease?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns the lease the request produced (whatever its status), or null if the request has not been activated.
    /// </summary>
    Task<AccessLease?> GetByAccessRequestIdAsync(Guid accessRequestId);

    /// <summary>
    /// Returns the caller's active lease for the cipher whose window contains <paramref name="now"/>, or null.
    /// </summary>
    Task<AccessLease?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Returns the caller's currently-active leases (status Active, window containing <paramref name="now"/>, not
    /// revoked) across every organization they belong to. Returns an empty collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now);

    /// <summary>
    /// Atomically creates an auto-approved <see cref="AccessRequest"/>, its automatic <see cref="AccessDecision"/>, and an
    /// active <see cref="AccessLease"/> in a single transaction. The three entities must already have their ids assigned.
    /// The automatic path's request, decision, and lease never diverge because they are written together here. When
    /// <paramref name="enforceSingleActiveLease"/> is true the transaction first checks the per-cipher singleton and
    /// rolls back without persisting anything if another active in-window lease exists for the cipher.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision,
        AccessLease lease, DateTime now, bool enforceSingleActiveLease);

    /// <summary>
    /// Race-safely mints the active lease for an approved human request, copying the request's window. The insert
    /// re-checks ownership, Approved status, an open window, and that the request has not already produced a lease;
    /// returns <see cref="AccessLeaseMintOutcome.PreconditionFailed"/> when any precondition no longer holds (e.g. a
    /// concurrent activation won). When <paramref name="enforceSingleActiveLease"/> is true and another active
    /// in-window lease already exists for the cipher, returns <see cref="AccessLeaseMintOutcome.SingleActiveLeaseConflict"/>
    /// without minting. The lease must already have its id assigned.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now,
        bool enforceSingleActiveLease);

    /// <summary>
    /// Atomically revokes an active lease (setting its revoked date and revoker) and records the revocation reason as
    /// a human <paramref name="auditDecision"/> against the lease's originating request. The decision must already
    /// have its id assigned.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessDecision auditDecision, DateTime now);
}
