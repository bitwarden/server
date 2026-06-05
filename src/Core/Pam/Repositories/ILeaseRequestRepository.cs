using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.Repositories;

public interface ILeaseRequestRepository
{
    Task<LeaseRequest> CreateAsync(LeaseRequest request);

    Task<LeaseRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns the caller's pending (unresolved) lease request for the cipher, or null if there is none.
    /// </summary>
    Task<LeaseRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId);

    /// <summary>
    /// Returns the pending approver-inbox rows for the given collections, joined with their denormalized display
    /// fields. An empty <paramref name="collectionIds"/> yields an empty result.
    /// </summary>
    Task<ICollection<InboxLeaseRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Returns the resolved approver-inbox rows (anything no longer pending) created on or after
    /// <paramref name="since"/> for the given collections. An empty <paramref name="collectionIds"/> yields an empty
    /// result.
    /// </summary>
    Task<ICollection<InboxLeaseRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since);

    /// <summary>
    /// Atomically transitions a pending request to <paramref name="status"/> (setting its resolved date) and records
    /// the approver's human <paramref name="decision"/>. Both entities must already have their ids assigned.
    /// </summary>
    Task ResolveWithDecisionAsync(LeaseRequest request, LeaseDecision decision, LeaseRequestStatus status, DateTime now);
}
