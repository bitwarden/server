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
    /// Returns every currently-active lease (status Active, window containing <paramref name="now"/>) on the given
    /// collections, across all members — the governance view over a set of caller-manageable collections. Returns an
    /// empty collection when none are active.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Returns the ended leases (status Expired or Revoked) on the given collections that ended on or after
    /// <paramref name="since"/> — the governance history view over a set of caller-manageable collections. A revoked
    /// lease's end is its revoked date; an expired lease's end is its not-after. Returns an empty collection when none
    /// qualify.
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
    /// Atomically revokes an active lease (setting its revoked date and revoker) and records the revocation reason as
    /// a human <paramref name="auditDecision"/> against the lease's originating request. The decision must already
    /// have its id assigned.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessDecision auditDecision, DateTime now);
}
