using Bit.HttpExtensions;

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
