using Bit.HttpExtensions;

using Bit.Pam.Entities;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// An access request with its denormalized requester identity, serving the approver inbox, the caller's own request
/// list, and the cipher access-state snapshot. <see cref="ExpiredAt"/> has no backing store in v1 and is always null;
/// <see cref="RuleId"/> is the rule pinned at submit (null for requests created before pinning existed).
/// </summary>
public class AccessRequestDetailsResponseModel : ResponseModel
{
    public AccessRequestDetailsResponseModel()
        : base("accessRequestDetails")
    {
    }

    /// <summary>
    /// Maps the denormalized read-model projection — the approver inbox, the caller's own request list, and the
    /// cipher access-state snapshot all read through this shape — onto the wire contract.
    /// </summary>
    public AccessRequestDetailsResponseModel(AccessRequestDetails details)
        : base("accessRequestDetails")
    {
        ArgumentNullException.ThrowIfNull(details);

        Id = details.Id;
        CipherId = details.CipherId;
        CollectionId = details.CollectionId;
        OrganizationId = details.OrganizationId;
        RequesterId = details.RequesterId;
        RuleId = details.RuleId;
        Status = details.Status.ToApiStatus(details.ProducedLeaseId.HasValue);
        LeaseNotBefore = details.NotBefore.AsUtc();
        LeaseNotAfter = details.NotAfter.AsUtc();
        Reason = details.Reason;
        SubmittedAt = details.CreationDate.AsUtc();
        ResolvedAt = details.ResolvedDate.AsUtc();
        // The request's full decision log, oldest first: one element per recorded decision (human or automatic).
        // Empty only while pending (no decision recorded yet).
        Decisions = details.Decisions
            .Select(d => new AccessRequestDecisionResponseModel
            {
                DeciderKind = d.DeciderKind.ToApiKind(),
                Id = d.Id,
                Name = d.Name,
                Email = d.Email,
                Comment = d.Comment,
                Verdict = d.Verdict.ToApiVerdict(),
                DecidedAt = d.DecidedAt.AsUtc(),
            })
            .ToList();
        ProducedLeaseId = details.ProducedLeaseId;
        ProducedLeaseStatus = details.ProducedLeaseStatus?.ToApiStatus();
        ExtensionOfLeaseId = details.ExtensionOfLeaseId;
        RequesterName = details.RequesterName;
        RequesterEmail = details.RequesterEmail;
    }

    /// <summary>
    /// Maps a bare <see cref="AccessRequest"/> onto the wire contract — the shape available at submit, before the
    /// server has denormalized the requester's identity or produced a lease (so <see cref="ProducedLeaseId"/>,
    /// <see cref="ProducedLeaseStatus"/>, <see cref="RequesterName"/>, and <see cref="RequesterEmail"/> stay null).
    /// <paramref name="decisions"/> defaults to empty; the caller supplies the single automatic decision for an
    /// auto-approved submission.
    /// </summary>
    public AccessRequestDetailsResponseModel(
        AccessRequest request,
        IEnumerable<AccessRequestDecisionResponseModel>? decisions = null)
        : base("accessRequestDetails")
    {
        ArgumentNullException.ThrowIfNull(request);

        Id = request.Id;
        CipherId = request.CipherId;
        CollectionId = request.CollectionId;
        OrganizationId = request.OrganizationId;
        RequesterId = request.RequesterId;
        RuleId = request.RuleId;
        Status = request.Status.ToApiStatus(hasLease: false);
        LeaseNotBefore = request.NotBefore.AsUtc();
        LeaseNotAfter = request.NotAfter.AsUtc();
        Reason = request.Reason;
        SubmittedAt = request.CreationDate.AsUtc();
        ResolvedAt = request.ResolvedDate.AsUtc();
        Decisions = decisions ?? Enumerable.Empty<AccessRequestDecisionResponseModel>();
        ExtensionOfLeaseId = request.ExtensionOfLeaseId;
    }

    /// <summary>The access request's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The cipher access was requested for.</summary>
    public Guid CipherId { get; set; }

    /// <summary>The collection the cipher belongs to, through which the request is governed.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>The organization that owns the cipher.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The member who opened the request.</summary>
    public Guid RequesterId { get; set; }

    /// <summary>
    /// The access rule that gated the cipher and that this request is evaluated against, resolved once at submit
    /// (oldest wins) and pinned on the request. Null for requests created before pinning existed.
    /// </summary>
    public Guid? RuleId { get; set; }

    /// <summary>The request's lifecycle state.</summary>
    public AccessRequestStatus Status { get; set; }

    /// <summary>
    /// The activation window resolved at submit — the bounds on WHEN this request may be promoted to a lease. Both
    /// request modes collapse into it (on-demand → <c>now</c>..<c>now + duration</c>, scheduled → the chosen
    /// start/end), so there is no separate duration or mode field; the length is <see cref="LeaseNotAfter"/> minus
    /// <see cref="LeaseNotBefore"/>. In v1 the approved and leased windows are identical to this one.
    /// </summary>
    public DateTime LeaseNotBefore { get; set; }

    /// <summary>The end of the resolved activation window (UTC); see <see cref="LeaseNotBefore"/>.</summary>
    public DateTime LeaseNotAfter { get; set; }

    /// <summary>The optional justification the requester supplied when opening the request.</summary>
    public string? Reason { get; set; }

    /// <summary>When the request was opened (UTC).</summary>
    public DateTime SubmittedAt { get; set; }

    /// <summary>When the request was approved, denied, or cancelled (UTC); null while pending.</summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>Distinct from <see cref="ResolvedAt"/>; set when an approved request lapses unactivated. Not tracked in v1.</summary>
    public DateTime? ExpiredAt { get; set; }

    /// <summary>
    /// The request's decision log, oldest first — one element per decision (human or automatic). Each carries who
    /// decided (<c>deciderKind</c>), the verdict, and (for a human decision) the approver's identity and comment.
    /// Empty only while pending. An array so multi-party approval lands without breaking the contract.
    /// </summary>
    public IEnumerable<AccessRequestDecisionResponseModel> Decisions { get; set; } = null!;

    /// <summary>Set once an approved request has produced a lease.</summary>
    public Guid? ProducedLeaseId { get; set; }

    /// <summary>
    /// The produced lease's status, or null when no lease exists. The inbox uses this to keep an ended lease out of
    /// the "active" group so it is not offered for revocation.
    /// </summary>
    public AccessLeaseStatus? ProducedLeaseStatus { get; set; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    /// <summary>The requester's display name, denormalized by the server; null only when the user could not be resolved.</summary>
    public string? RequesterName { get; set; }

    /// <summary>The requester's email, denormalized by the server; null only when the user could not be resolved.</summary>
    public string? RequesterEmail { get; set; }
}
