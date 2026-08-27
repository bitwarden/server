using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest> CreateAsync(AccessRequest request);

    /// <summary>
    /// Atomically creates an auto-approved <see cref="AccessRequest"/> (action
    /// <see cref="AccessRequestAction.Approved"/>, stamped now) and its automatic <see cref="AccessDecision"/> in a
    /// single transaction. No lease is minted: the
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
    /// <paramref name="now"/> projects the produced lease's status — see
    /// <see cref="AccessRequestDetails.ProducedLeaseStatus"/>.
    /// </summary>
    Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id, DateTime now);

    /// <summary>
    /// Returns the caller's open lease request for the cipher (no action recorded) whose window has not lapsed
    /// (NotAfter after <paramref name="now"/>), or null if there is none. A lapsed unanswered request is derived
    /// Expired and no longer blocks a fresh submission.
    /// </summary>
    Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Returns the caller's approved-but-not-yet-activated request for the cipher whose window has not lapsed
    /// (NotAfter after <paramref name="now"/>), or null. Future windows are included so the client can show the
    /// upcoming window; a request that has produced a lease is activated, not approved, and is excluded.
    /// </summary>
    Task<AccessRequest?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Returns the caller's own lease requests across every organization they belong to, most recent first and capped
    /// server-side. Display-name fields are not populated for this caller-scoped surface.
    /// <paramref name="since"/> bounds the history window, matching the approver-side reads (PM-42614); rows that are
    /// still live — pending, or approved with a window that has not lapsed as of <paramref name="now"/> — are returned
    /// regardless of age, because a request the caller can still act on is not history. A null
    /// <paramref name="since"/> means no window at all. <paramref name="now"/> both decides that unlapsed-ness and
    /// projects each produced lease's status — see <see cref="AccessRequestDetails.ProducedLeaseStatus"/>.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId, DateTime? since, DateTime now);

    /// <summary>
    /// Returns the pending approver-inbox rows for the given collections, joined with their denormalized display
    /// fields. Only actionable rows qualify: no action recorded and a window still open as of
    /// <paramref name="now"/> — a lapsed row is derived Expired and belongs to the history read instead. An empty
    /// <paramref name="collectionIds"/> yields an empty result.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// Returns the non-actionable approver-inbox rows (an action recorded, or a window lapsed as of
    /// <paramref name="now"/> — the derived-Expired complement of the pending read) created on or after
    /// <paramref name="since"/> for the given collections. An empty <paramref name="collectionIds"/> yields an empty
    /// result. <paramref name="since"/> bounds the history window; <paramref name="now"/> is the separate read clock
    /// that projects each produced lease's status — see <see cref="AccessRequestDetails.ProducedLeaseStatus"/>.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since, DateTime now);

    /// <summary>
    /// Atomically records <paramref name="action"/> (with its action date) on a request that has none yet, and the
    /// approver's human <paramref name="decision"/>. The guarded UPDATE is the concurrency token: the decision is
    /// written only when the transition happened, so a losing approver's verdict never enters the log. No lease is
    /// created here: the requester activates an approved request later via
    /// <see cref="IAccessLeaseRepository.CreateFromApprovedRequestAsync"/>. Both supplied entities must already have
    /// their ids assigned.
    /// </summary>
    Task ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestAction action, DateTime now);

    /// <summary>
    /// Withdraws a not-yet-activated request on the requester's behalf: records
    /// <see cref="AccessRequestAction.Cancelled"/> (over <see cref="AccessRequestAction.None"/> or an
    /// <see cref="AccessRequestAction.Approved"/> the requester has not activated) and stamps
    /// <paramref name="now"/> as its action date. No <see cref="AccessDecision"/> is written — a cancellation is the
    /// requester acting on their own request, not an approver verdict. The write is guarded so a request that has
    /// already left the cancellable set, produced a lease, or whose window has lapsed (a row users saw as Expired
    /// must not later restamp) is left untouched (race-safe / idempotent).
    /// </summary>
    Task CancelAsync(Guid id, DateTime now);

    /// <summary>
    /// Retracts a not-yet-activated request on a managing approver's behalf: records
    /// <see cref="AccessRequestAction.Denied"/> (over <see cref="AccessRequestAction.None"/> or an unactivated
    /// <see cref="AccessRequestAction.Approved"/>), stamps the action date, and records the approver's human
    /// Deny <paramref name="decision"/> so the audit trail names them. The write is guarded so a request that has
    /// already left the cancellable set, produced a lease, or whose window has lapsed is left untouched (race-safe);
    /// the decision is recorded only when the transition happens. Both supplied entities must already have their ids assigned.
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
    /// <param name="denialComment">
    /// The comment recorded on the automatic Deny decision when the lease is no longer extendable. The
    /// <see cref="AccessLeaseExtendOutcome.LeaseNotActive"/> path is not a silent refusal: it still writes the request
    /// — Denied rather than Approved, with this comment naming why — so the requester can inspect what they asked for
    /// (PM-42632). Only the outcome distinguishes the two writes; the caller supplies one set of entities for both.
    /// </param>
    Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(AccessRequest request, AccessDecision decision,
        DateTime now, string? denialComment);
}
