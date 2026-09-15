using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// A lease request projected for the approver inbox: every <see cref="Entities.AccessRequest"/> field plus the
/// denormalized requester identity the client needs, the lease the request produced (if any), and the human resolver's
/// identity/comment. Populated by a single join in the read procedures so the client avoids an N+1.
/// </summary>
public class AccessRequestDetails
{
    public Guid Id { get; set; }

    /// <summary>The parent lease for an extension request; null for original requests.</summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>The access rule pinned on the request at submit, or null for requests created before pinning.</summary>
    public Guid? RuleId { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.NotBefore"/>
    public DateTime NotBefore { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.NotAfter"/>
    public DateTime NotAfter { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.Reason"/>
    public string? Reason { get; set; }

    /// <summary>
    /// The request's status as of the clock the read was given — the stored <see cref="Enums.AccessRequestAction"/>
    /// interpreted against that clock by <see cref="Enums.AccessStatusDerivation.ComputeStatus"/>, stamped at the
    /// repository boundary. The stored action itself is never exposed on a read model.
    /// </summary>
    public AccessRequestStatus Status { get; set; }

    /// <inheritdoc cref="Entities.AccessRequest.CreationDate"/>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When a party resolved the request (<see cref="Entities.AccessRequest.ActionDate"/>); null while no action is
    /// recorded, including for derived-Expired rows. Keeps the wire's <c>resolvedAt</c> name and meaning.
    /// </summary>
    public DateTime? ResolvedDate { get; set; }

    /// <summary>The lease this request produced once activated, or null if it has not produced a lease.</summary>
    public Guid? ProducedLeaseId { get; set; }

    /// <summary>
    /// The produced lease's status as of the clock the read was given, or null when the request has not produced a
    /// lease. Lets the inbox distinguish a still-live lease from one that has ended, so an ended lease is not offered
    /// for revocation.
    /// </summary>
    /// <remarks>
    /// Derived, not stored, off the lease's own <c>NotAfter</c> rather than the request's, since an extension
    /// pushes the lease's end out in place. See <see cref="Enums.AccessStatusDerivation.ComputeLeaseStatus"/>.
    /// </remarks>
    public AccessLeaseStatus? ProducedLeaseStatus { get; set; }

    /// <summary>
    /// Every decision recorded against this request, oldest first. Empty while pending, and for the terminal
    /// states that record no verdict: a requester cancellation and <see cref="AccessRequestStatus.Expired"/>.
    /// </summary>
    public List<AccessRequestDecision> Decisions { get; set; } = new();

    /// <summary>The requester's display name, denormalized from the User join; null when unset or the user could not be resolved.</summary>
    public string? RequesterName { get; set; }

    /// <summary>The requester's email, the fallback display when <see cref="RequesterName"/> is unset.</summary>
    public string? RequesterEmail { get; set; }

    /// <summary>
    /// Projects an <see cref="Entities.AccessRequest"/> the caller just wrote (or read scoped to itself) onto the
    /// read model, deriving <see cref="Status"/> against <paramref name="now"/> exactly as the repository reads do.
    /// The lease fields stay null since no produced lease can exist at these call sites.
    /// </summary>
    public static AccessRequestDetails From(Entities.AccessRequest request, DateTime now)
    {
        var details = new AccessRequestDetails
        {
            Id = request.Id,
            ExtensionOfLeaseId = request.ExtensionOfLeaseId,
            OrganizationId = request.OrganizationId,
            CollectionId = request.CollectionId,
            CipherId = request.CipherId,
            RequesterId = request.RequesterId,
            RuleId = request.RuleId,
            NotBefore = request.NotBefore,
            NotAfter = request.NotAfter,
            Reason = request.Reason,
            CreationDate = request.CreationDate,
            ResolvedDate = request.ActionDate,
        };
        details.StampDerivedStatuses(request.Action, producedLease: null, now);
        return details;
    }

    /// <summary>
    /// Stamps the derived statuses from stored facts: the request's status from <paramref name="action"/>, and the
    /// produced lease's (when one exists) from the lease's <em>own</em> action and NotAfter, since an extension
    /// pushes the lease's end out in place. <see cref="NotAfter"/> and <see cref="ExtensionOfLeaseId"/> must already
    /// be set.
    /// </summary>
    public void StampDerivedStatuses(AccessRequestAction action,
        (Guid Id, AccessLeaseAction Action, DateTime NotAfter)? producedLease, DateTime now)
    {
        Status = AccessStatusDerivation.ComputeStatus(
            action, hasLease: producedLease is not null, isExtension: ExtensionOfLeaseId is not null,
            NotAfter, now);
        ProducedLeaseId = producedLease?.Id;
        ProducedLeaseStatus = producedLease is { } lease
            ? AccessStatusDerivation.ComputeLeaseStatus(lease.Action, lease.NotAfter, now)
            : null;
    }
}
