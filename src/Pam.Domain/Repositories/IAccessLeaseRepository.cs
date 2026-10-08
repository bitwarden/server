using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessLeaseRepository
{
    Task<AccessLease?> GetByIdAsync(Guid id);

    /// <summary>The lease the request produced, whatever its status.</summary>
    Task<AccessLease?> GetByAccessRequestIdAsync(Guid accessRequestId);

    Task<AccessLease?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>The caller's active leases across every organization.</summary>
    Task<ICollection<AccessLease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now);

    /// <summary>
    /// The active lease on the cipher that ends last, across all members. Cipher-scoped like the singleton guard,
    /// since the holder may reach it through a collection the caller cannot.
    /// </summary>
    Task<AccessLease?> GetActiveByCipherIdAsync(Guid cipherId, DateTime now);

    /// <summary>Every active lease on the collections, across all members.</summary>
    Task<ICollection<AccessLease>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Leases on the collections that ended (expired, revoked, or cancelled) on or after <paramref name="since"/>.
    /// Expiry is derived against <paramref name="now"/>, since it is never stored.
    /// </summary>
    Task<ICollection<AccessLease>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since,
        DateTime now);

    /// <summary>
    /// Race-safely mints the lease for an approved request, running from <paramref name="now"/> to the request's end.
    /// </summary>
    Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now,
        bool enforceSingleActiveLease);

    /// <summary>
    /// Atomically ends a running lease with <paramref name="endAction"/> and records <paramref name="auditDecision"/>
    /// against its originating request. A lease already ended or lapsed is left untouched.
    /// </summary>
    Task RevokeAsync(AccessLease lease, AccessLeaseAction endAction, AccessDecision auditDecision, DateTime now);

    /// <summary>Returns each lease whose window closed on its own and that no earlier sweep returned.</summary>
    Task<IReadOnlyList<PamExpiredLease>> ExpireDueAsync(DateTime now);
}
