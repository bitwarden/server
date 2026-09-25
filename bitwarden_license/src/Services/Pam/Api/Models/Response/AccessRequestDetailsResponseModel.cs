using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// An access request with its denormalized requester identity.
/// </summary>
public class AccessRequestDetailsResponseModel : ResponseModel
{
    public AccessRequestDetailsResponseModel()
        : base("accessRequestDetails")
    {
    }

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
        Status = details.Status;
        LeaseNotBefore = details.NotBefore.AsUtc();
        LeaseNotAfter = details.NotAfter.AsUtc();
        Reason = details.Reason;
        SubmittedAt = details.CreationDate.AsUtc();
        ResolvedAt = details.ActionDate.AsUtc();
        // Oldest first; empty while pending.
        Decisions = details.Decisions
            .Select(d => new AccessRequestDecisionResponseModel
            {
                DeciderKind = d.DeciderKind,
                Id = d.ApproverId,
                Name = d.Name,
                Email = d.Email,
                Comment = d.Comment,
                Verdict = d.Verdict,
                DecidedAt = d.DecidedAt.AsUtc(),
            })
            .ToList();
        ProducedLeaseId = details.ProducedLeaseId;
        ProducedLeaseStatus = details.ProducedLeaseStatus;
        ProducedLeaseNotAfter = details.ProducedLeaseNotAfter.AsUtc();
        ExtensionOfLeaseId = details.ExtensionOfLeaseId;
        RequesterName = details.RequesterName;
        RequesterEmail = details.RequesterEmail;
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
    /// The access rule pinned on the request at submit, if any.
    /// </summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// The request's lifecycle state as of the read clock. An expired request whose <see cref="Decisions"/> hold an
    /// approval was approved but never activated.
    /// </summary>
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

    /// <summary>
    /// When a party approved, denied, or cancelled the request (UTC). Null while pending or expired.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

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

    /// <summary>
    /// The produced lease's end (UTC), including any extension, or null when no lease exists.
    /// </summary>
    public DateTime? ProducedLeaseNotAfter { get; set; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    /// <summary>The requester's display name, denormalized by the server; null only when the user could not be resolved.</summary>
    public string? RequesterName { get; set; }

    /// <summary>The requester's email, denormalized by the server; null only when the user could not be resolved.</summary>
    public string? RequesterEmail { get; set; }
}
