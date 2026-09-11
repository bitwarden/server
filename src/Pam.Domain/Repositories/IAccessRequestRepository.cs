using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest> CreateAsync(AccessRequest request);

    /// <summary>
    /// Atomically creates an auto-approved <see cref="AccessRequest"/> and its automatic <see cref="AccessDecision"/>
    /// in a single transaction. No lease is minted: the requester activates later via
    /// <see cref="IAccessLeaseRepository.CreateFromApprovedRequestAsync"/>, same as the human path.
    /// </summary>
    Task CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision);

    Task<AccessRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Returns a single request's full <see cref="AccessRequestDetails"/> projection for the dedicated request
    /// page, or null if none. Unlike <see cref="GetByIdAsync"/> this populates the display-name fields.
    /// Authorization is enforced by the calling query, not this read.
    /// </summary>
    Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id, DateTime now);

    /// <summary>
    /// Returns the caller's open lease request for the cipher whose window has not lapsed, or null. A lapsed
    /// unanswered request is derived Expired and does not block a fresh submission.
    /// </summary>
    Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Returns the caller's approved-but-not-yet-activated request for the cipher whose window has not lapsed
    /// (NotAfter after <paramref name="now"/>), or null. Future windows are included so the client can show the
    /// upcoming window; a request that has produced a lease is activated, not approved, and is excluded.
    /// </summary>
    Task<AccessRequest?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// Returns the caller's own lease requests across every organization, most recent first and capped server-side.
    /// <paramref name="since"/> bounds the history window, matching the approver-side reads; live rows (pending, or
    /// approved and unlapsed) are returned regardless of age. <paramref name="now"/> also projects each produced
    /// lease's status.
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
    /// Returns the non-actionable approver-inbox rows (an action recorded, or a lapsed window — the derived-Expired
    /// complement of the pending read) created on or after <paramref name="since"/>. <paramref name="now"/> also
    /// projects each produced lease's status.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since, DateTime now);

    /// <summary>
    /// Atomically records <paramref name="action"/> on a request that has none yet, plus the approver's human
    /// <paramref name="decision"/>. The guarded UPDATE is the concurrency token: a losing approver's verdict never
    /// enters the log. No lease is created here; the requester activates later via
    /// <see cref="IAccessLeaseRepository.CreateFromApprovedRequestAsync"/>.
    /// </summary>
    Task ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestAction action, DateTime now);

    /// <summary>
    /// Withdraws a not-yet-activated request on the requester's behalf: records
    /// <see cref="AccessRequestAction.Cancelled"/> and stamps <paramref name="now"/> as its action date. No
    /// <see cref="AccessDecision"/> is written, since this isn't an approver verdict. Guarded to stay idempotent
    /// under a race.
    /// </summary>
    Task CancelAsync(Guid id, DateTime now);

    /// <summary>
    /// Retracts a not-yet-activated request on a managing approver's behalf: records
    /// <see cref="AccessRequestAction.Denied"/> and the approver's human Deny <paramref name="decision"/>. Guarded
    /// so a request that has produced a lease or whose window has lapsed is left untouched.
    /// </summary>
    Task CancelWithDecisionAsync(AccessRequest request, AccessDecision decision, DateTime now);

    /// <summary>
    /// Returns the number of extension requests recorded against the lease (a lease may be extended once, so this is
    /// 0 or 1). Used to gate whether another extension is allowed.
    /// </summary>
    Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId);

    /// <summary>
    /// Atomically records an auto-approved extension request and pushes the parent lease's end out to the
    /// request's NotAfter, under a per-lease lock. Returns <see cref="AccessLeaseExtendOutcome.LeaseNotActive"/>,
    /// <see cref="AccessLeaseExtendOutcome.AlreadyExtended"/>, or <see cref="AccessLeaseExtendOutcome.Extended"/>.
    /// </summary>
    /// <param name="denialComment">
    /// The comment recorded on the automatic Deny decision when the lease is no longer extendable.
    /// </param>
    Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(AccessRequest request, AccessDecision decision,
        DateTime now, string? denialComment);
}
