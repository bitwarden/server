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
    /// Returns the caller's active leases (no early end, window containing <paramref name="now"/>) across every
    /// organization.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now);

    /// <summary>
    /// Returns the active lease on the cipher that ends <em>last</em>, across all members, or null when free.
    /// </summary>
    /// <remarks>
    /// Deliberately cipher-scoped, mirroring the singleton guard's own filter, since scoping to the caller's
    /// reachable collections would miss a holder whose path the caller cannot reach.
    /// </remarks>
    Task<AccessLease?> GetActiveByCipherIdAsync(Guid cipherId, DateTime now);

    /// <summary>
    /// Returns every active lease on the given collections, across all members — the governance view over a set of
    /// caller-manageable collections.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Returns the ended leases (Expired, Revoked, or Cancelled) on the given collections that ended on or after
    /// <paramref name="since"/>.
    /// </summary>
    /// <remarks>
    /// Expiry is never stored, so ended-ness is derived by composing the recorded action with a clock comparison
    /// against <paramref name="now"/>.
    /// </remarks>
    Task<ICollection<AccessLease>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since,
        DateTime now);

    /// <summary>
    /// Race-safely mints the lease for an approved request, copying its window. Returns
    /// <see cref="AccessLeaseMintOutcome.PreconditionFailed"/> for a stale precondition, or
    /// <see cref="AccessLeaseMintOutcome.SingleActiveLeaseConflict"/> when <paramref name="enforceSingleActiveLease"/>
    /// finds another active lease on the cipher.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now,
        bool enforceSingleActiveLease);

    /// <summary>
    /// Atomically ends a running lease with <paramref name="endAction"/> (Revoked or Cancelled) and records
    /// <paramref name="auditDecision"/> against the lease's originating request.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessLeaseAction endAction, AccessDecision auditDecision, DateTime now);

    /// <summary>
    /// Deviation: no ground-truth interface declared the natural-expiry sweep, so it lives here, alongside
    /// <see cref="RevokeAsync"/>, rather than on the rotation-job-shaped <c>IPamRotationJobRepository</c>. Returns
    /// one row per lease whose window closed on its own that the sweep has not returned before, for the caller's
    /// LeaseExpired audit emission / access-end rotation trigger.
    /// </summary>
    Task<IReadOnlyList<PamExpiredLease>> ExpireDueAsync(DateTime now);
}
