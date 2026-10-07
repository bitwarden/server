using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Pam.Repositories;

public interface IAccessRequestRepository
{
    Task<AccessRequest> CreateAsync(AccessRequest request);

    /// <summary>
    /// Atomically creates an auto-approved request and its automatic decision. No lease is minted; the requester
    /// activates the request later, as on the human path.
    /// </summary>
    Task CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision);

    Task<AccessRequest?> GetByIdAsync(Guid id);

    /// <summary>
    /// Unlike <see cref="GetByIdAsync"/>, populates the display-name fields. The calling query enforces authorization.
    /// </summary>
    Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id, DateTime now);

    /// <summary>
    /// The caller's open request for the cipher. A lapsed one is derived Expired, so it does not block a fresh
    /// submission.
    /// </summary>
    Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// The caller's approved, unactivated request for the cipher whose window has not lapsed. A future window is
    /// included so the client can show it.
    /// </summary>
    Task<AccessRequest?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now);

    /// <summary>
    /// The caller's requests across every organization, newest first and capped. <paramref name="since"/> bounds the
    /// history, but live rows (pending, or approved and unlapsed) are returned regardless of age.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId, DateTime? since, DateTime now);

    /// <summary>
    /// The approver inbox's actionable rows: no action recorded and the window still open. A lapsed row is derived
    /// Expired and belongs to the history read.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now);

    /// <summary>
    /// The complement of the pending read (an action recorded, or a lapsed window), created on or after
    /// <paramref name="since"/>.
    /// </summary>
    Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since, DateTime now);

    /// <summary>
    /// Atomically records <paramref name="action"/> and the approver's <paramref name="decision"/> on an open,
    /// unlapsed request. The guarded UPDATE is the concurrency token, so a losing approver's verdict never enters the
    /// log.
    /// </summary>
    /// <returns><c>false</c> when the request was no longer open.</returns>
    Task<bool> ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestAction action, DateTime now);

    /// <summary>
    /// Withdraws a not-yet-activated request for its requester. No decision is written, since this is not an
    /// approver verdict.
    /// </summary>
    /// <returns><c>false</c> when the request was no longer withdrawable.</returns>
    Task<bool> CancelAsync(Guid id, DateTime now);

    /// <summary>
    /// Retracts a not-yet-activated request for a managing approver, recording Denied and the approver's Deny
    /// <paramref name="decision"/>.
    /// </summary>
    /// <returns><c>false</c> when the request was no longer retractable.</returns>
    Task<bool> CancelWithDecisionAsync(AccessRequest request, AccessDecision decision, DateTime now);

    /// <summary>Counts the lease's extension requests, denied ones included; a lease may be extended once.</summary>
    Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId);

    /// <summary>
    /// Under a per-lease lock, records an auto-approved extension request and pushes the lease's end out to the
    /// request's <c>NotAfter</c>.
    /// </summary>
    /// <param name="denialComment">Recorded on the automatic Deny decision when the lease has ended.</param>
    Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(AccessRequest request, AccessDecision decision,
        DateTime now, string? denialComment);
}
