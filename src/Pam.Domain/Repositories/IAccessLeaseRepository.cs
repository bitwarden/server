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
    /// Returns the caller's currently-active leases (no early end recorded, window containing
    /// <paramref name="now"/>) across every organization they belong to. Returns an empty collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now);

    /// <summary>
    /// Returns the active lease (no early end recorded, window containing <paramref name="now"/>) on the cipher that
    /// ends <em>last</em>, across all members, or null when the cipher is free. Answers "is this cipher's
    /// single-active-lease slot taken, and when does it free" for a caller who holds no lease of their own.
    /// </summary>
    /// <remarks>
    /// Deliberately cipher-scoped, mirroring the singleton guard inside
    /// <see cref="CreateFromApprovedRequestAsync"/>'s procedure: that guard filters on the cipher alone and ignores
    /// <see cref="AccessLease.CollectionId"/>, so answering this from
    /// <see cref="GetManyActiveByCollectionIdsAsync"/> over the caller's reachable collections would miss a holder
    /// whose path the caller cannot reach and would call a taken slot free.
    ///
    /// Latest-ending rather than first, because the guard blocks while <em>any</em> in-window lease exists. More than
    /// one is routine: enforcement is decided per-caller, so a member with an escape path mints without the guard
    /// even while a constrained member holds one.
    /// </remarks>
    Task<AccessLease?> GetActiveByCipherIdAsync(Guid cipherId, DateTime now);

    /// <summary>
    /// Returns every currently-active lease (no early end recorded, window containing <paramref name="now"/>) on
    /// the given collections, across all members — the governance view over a set of caller-manageable collections. Returns an
    /// empty collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Returns the ended leases (Expired, Revoked, or Cancelled as of <paramref name="now"/>) on the given
    /// collections that ended on or after <paramref name="since"/> — the governance history view over a set of
    /// caller-manageable collections. A revoked/cancelled lease's end is its revoked date; an expired lease's end is
    /// its not-after. Returns an empty collection when none qualify.
    /// </summary>
    /// <remarks>
    /// Ended-ness is derived against <paramref name="now"/>: expiry is never stored (a lease whose window merely
    /// closed carries <see cref="AccessLeaseAction.None"/> forever), so the filter composes the recorded action with
    /// a plain clock comparison. The returned entities expose the stored fact only; callers derive the status via
    /// <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>.
    /// </remarks>
    Task<ICollection<AccessLease>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since,
        DateTime now);

    /// <summary>
    /// Race-safely mints the lease for an approved request, copying the request's window. The insert
    /// re-checks ownership, a recorded approval, an open window, and that the request has not already produced a lease;
    /// returns <see cref="AccessLeaseMintOutcome.PreconditionFailed"/> when any precondition no longer holds (e.g. a
    /// concurrent activation won). When <paramref name="enforceSingleActiveLease"/> is true and another active
    /// in-window lease already exists for the cipher, returns <see cref="AccessLeaseMintOutcome.SingleActiveLeaseConflict"/>
    /// without minting. The lease must already have its id assigned.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now,
        bool enforceSingleActiveLease);

    /// <summary>
    /// Atomically ends a running lease — recording <paramref name="endAction"/> (Revoked when an operator ended it,
    /// Cancelled when the holder ended their own) along with its revoked date and revoker — and records the reason
    /// as a human <paramref name="auditDecision"/> against the lease's originating request. The guarded UPDATE only
    /// matches a lease with no early end yet, so a repeat or losing revoke ends nothing and appends nothing. The
    /// decision must already have its id assigned.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessLeaseAction endAction, AccessDecision auditDecision, DateTime now);

    /// <summary>
    /// Deviation: no interface in the ground-truth contract declared the natural-expiry sweep
    /// (<c>AccessLease_ExpireDue</c>), even though the sproc exists. Added here — rather than on
    /// <c>IPamRotationJobRepository</c>, whose sweeps are all rotation-job-shaped — because the sproc operates
    /// purely on <see cref="AccessLease"/> and sits naturally alongside <see cref="RevokeAsync"/>, the other
    /// lease-ending write. Returns one row per lease whose window closed on its own (<c>NotAfter &lt;= now</c>,
    /// no early end recorded) that the sweep has not returned before, for the caller's LeaseExpired audit
    /// emission / access-end rotation trigger.
    /// </summary>
    Task<IReadOnlyList<PamExpiredLease>> ExpireDueAsync(DateTime now);
}
