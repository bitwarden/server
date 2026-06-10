using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;

namespace Bit.Core.Pam.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest> CreateAsync(AccessRequest request);

    Task<AccessRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns the caller's pending (unresolved) lease request for the cipher, or null if there is none.
    /// </summary>
    Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId);

    /// <summary>
    /// Returns the caller's own lease requests across every organization they belong to, regardless of status, most
    /// recent first and capped server-side. Display-name fields are not populated for this caller-scoped surface.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId);

    /// <summary>
    /// Returns the pending approver-inbox rows for the given collections, joined with their denormalized display
    /// fields. An empty <paramref name="collectionIds"/> yields an empty result.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Returns the resolved approver-inbox rows (anything no longer pending) created on or after
    /// <paramref name="since"/> for the given collections. An empty <paramref name="collectionIds"/> yields an empty
    /// result.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since);

    /// <summary>
    /// Atomically transitions a pending request to <paramref name="status"/> (setting its resolved date), records the
    /// approver's human <paramref name="decision"/>, and — on approval — creates the active <paramref name="lease"/>
    /// that authorizes access, spanning the request's approved window. Pass <paramref name="lease"/> as null when
    /// denying. Every supplied entity must already have its id assigned.
    /// </summary>
    Task ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestStatus status, AccessLease? lease, DateTime now);
}
