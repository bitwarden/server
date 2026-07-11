using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

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
    /// Returns every currently-active lease (status Active, window containing <paramref name="now"/>) on the given
    /// collections, across all members — the governance view over a set of caller-manageable collections. Returns an
    /// empty collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Returns the ended leases (status Expired, Revoked, or Cancelled) on the given collections that ended on or after
    /// <paramref name="since"/> — the governance history view over a set of caller-manageable collections. A
    /// revoked/cancelled lease's end is its revoked date; an expired lease's end is its not-after. Returns an empty
    /// collection when none qualify.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since);

    /// <summary>
    /// Race-safely mints the active lease for an approved request, copying the request's window. The insert
    /// re-checks ownership, Approved status, an open window, and that the request has not already produced a lease;
    /// returns <see cref="AccessLeaseMintOutcome.PreconditionFailed"/> when any precondition no longer holds (e.g. a
    /// concurrent activation won). When <paramref name="enforceSingleActiveLease"/> is true and another active
    /// in-window lease already exists for the cipher, returns <see cref="AccessLeaseMintOutcome.SingleActiveLeaseConflict"/>
    /// without minting. The lease must already have its id assigned.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now,
        bool enforceSingleActiveLease);

    /// <summary>
    /// Atomically ends an active lease — setting its status to <paramref name="endStatus"/> (Revoked when an operator
    /// ended it, Cancelled when the holder ended their own) along with its revoked date and revoker — and records the
    /// reason as a human <paramref name="auditDecision"/> against the lease's originating request. The decision must
    /// already have its id assigned.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessLeaseStatus endStatus, AccessDecision auditDecision, DateTime now);

    /// <summary>
    /// Deviation: no interface in the ground-truth contract declared the natural-expiry sweep
    /// (<c>AccessLease_ExpireDue</c>), even though the sproc exists. Added here — rather than on
    /// <c>IPamRotationJobRepository</c>, whose sweeps are all rotation-job-shaped — because the sproc operates
    /// purely on <see cref="AccessLease"/> and sits naturally alongside <see cref="RevokeAsync"/>, the other
    /// lease-ending write. Flips every <see cref="AccessLeaseStatus.Active"/> lease whose window closed on its own
    /// (<c>NotAfter &lt;= now</c>) to <see cref="AccessLeaseStatus.Expired"/>, returning one row per expired lease
    /// for the caller's (deferred) LeaseExpired audit emission / access-end rotation trigger.
    /// </summary>
    Task<IReadOnlyList<PamExpiredLease>> ExpireDueAsync(DateTime now);
}
