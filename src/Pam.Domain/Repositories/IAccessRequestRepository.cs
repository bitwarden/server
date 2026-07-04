using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest> CreateAsync(AccessRequest request);

    /// <summary>
    /// Atomically creates an auto-approved <see cref="AccessRequest"/> (status <see cref="AccessRequestStatus.Approved"/>,
    /// resolved now) and its automatic <see cref="AccessDecision"/> in a single transaction. No lease is minted: the
    /// requester activates the approved request later via <see cref="IAccessLeaseRepository.CreateFromApprovedRequestAsync"/>,
    /// just like the human path after approval. Both supplied entities must already have their ids assigned.
    /// </summary>
    Task CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision);

    Task<AccessRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns a single request's full <see cref="AccessRequestDetails"/> projection (denormalized display fields,
    /// produced lease, and the complete decision list) for the dedicated request page, or null if no request has the
    /// id. Unlike <see cref="GetByIdAsync"/> this populates the display-name fields. Authorization (the caller is the
    /// requester or can manage the request's collection) is enforced by the calling query, not this read.
    /// </summary>
    Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id);

    /// <summary>
    /// Returns the caller's pending (unresolved) lease request for the cipher, or null if there is none.
    /// </summary>
    Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId);

    /// <summary>
    /// Returns the caller's approved-but-not-yet-activated request for the cipher whose window has not lapsed
    /// (NotAfter after <paramref name="now"/>), or null. Future windows are included so the client can show the
    /// upcoming window; a request that has produced a lease is activated, not approved, and is excluded.
    /// </summary>
    Task<AccessRequest?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

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
    /// Atomically transitions a pending request to <paramref name="status"/> (setting its resolved date) and records
    /// the approver's human <paramref name="decision"/>. No lease is created here: the requester activates an
    /// approved request later via <see cref="IAccessLeaseRepository.CreateFromApprovedRequestAsync"/>. Both supplied
    /// entities must already have their ids assigned.
    /// </summary>
    Task ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestStatus status, DateTime now);

    /// <summary>
    /// Withdraws a not-yet-activated request on the requester's behalf: transitions it to
    /// <see cref="AccessRequestStatus.Cancelled"/> (from <see cref="AccessRequestStatus.Pending"/> or an
    /// <see cref="AccessRequestStatus.Approved"/> request the requester has not activated) and stamps
    /// <paramref name="now"/> as its resolved date. No <see cref="AccessDecision"/> is written — a cancellation is the
    /// requester acting on their own request, not an approver verdict. The write is guarded so a request that has
    /// already left the cancellable set or produced a lease is left untouched (race-safe / idempotent).
    /// </summary>
    Task CancelAsync(Guid id, DateTime now);

    /// <summary>
    /// Retracts a not-yet-activated request on a managing approver's behalf: transitions it to
    /// <see cref="AccessRequestStatus.Denied"/> (from <see cref="AccessRequestStatus.Pending"/> or an unactivated
    /// <see cref="AccessRequestStatus.Approved"/> request), stamps the resolved date, and records the approver's human
    /// Deny <paramref name="decision"/> so the audit trail names them. The write is guarded so a request that has
    /// already left the cancellable set or produced a lease is left untouched (race-safe); the decision is recorded
    /// only when the transition happens. Both supplied entities must already have their ids assigned.
    /// </summary>
    Task CancelWithDecisionAsync(AccessRequest request, AccessDecision decision, DateTime now);

    /// <summary>
    /// Returns the number of extension requests recorded against the lease (a lease may be extended once, so this is
    /// 0 or 1). Used to gate whether another extension is allowed.
    /// </summary>
    Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId);

    /// <summary>
    /// Atomically records an auto-approved extension request (with its automatic decision) and pushes the parent
    /// lease's end out to the request's NotAfter, all under a per-lease lock. Returns
    /// <see cref="AccessLeaseExtendOutcome.LeaseNotActive"/> when the lease is no longer active or its window has
    /// ended, or <see cref="AccessLeaseExtendOutcome.AlreadyExtended"/> when the lease has already been extended (a
    /// lease may be extended once); otherwise <see cref="AccessLeaseExtendOutcome.Extended"/>. Both supplied entities
    /// must already have their ids assigned, and the request's <c>ExtensionOfLeaseId</c> identifies the lease being
    /// extended.
    /// </summary>
    Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(AccessRequest request, AccessDecision decision,
        DateTime now);
}
