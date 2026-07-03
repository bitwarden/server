using Bit.HttpExtensions;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// An access request with its denormalized requester identity, serving the approver inbox, the caller's own request
/// list, and the cipher access-state snapshot. <see cref="ExpiredAt"/> has no backing store in v1 and is always null;
/// <see cref="RuleId"/> is the rule pinned at submit (null for requests created before pinning existed).
/// </summary>
public class AccessRequestDetailsResponseModel : ResponseModel
{
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
        Status = AccessRequestStatusNames.From(details.Status, details.ProducedLeaseId.HasValue);
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
                DeciderKind = AccessDeciderKindNames.From(d.DeciderKind),
                Id = d.Id,
                Name = d.Name,
                Email = d.Email,
                Comment = d.Comment,
                Verdict = d.Verdict,
                DecidedAt = d.DecidedAt.AsUtc(),
            })
            .ToList();
        ProducedLeaseId = details.ProducedLeaseId;
        ProducedLeaseStatus = details.ProducedLeaseStatus.HasValue
            ? AccessLeaseStatusNames.From(details.ProducedLeaseStatus.Value)
            : null;
        ExtensionOfLeaseId = details.ExtensionOfLeaseId;
        RequesterName = details.RequesterName;
        RequesterEmail = details.RequesterEmail;
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }

    public Guid OrganizationId { get; }
    public Guid RequesterId { get; }

    /// <summary>
    /// The access rule that gated the cipher and that this request is evaluated against, resolved once at submit
    /// (oldest wins) and pinned on the request. Null for requests created before pinning existed.
    /// </summary>
    public Guid? RuleId { get; }

    /// <summary><c>pending | approved | activated | denied | cancelled | expired</c>.</summary>
    public string Status { get; }

    /// <summary>
    /// The activation window resolved at submit — the bounds on WHEN this request may be promoted to a lease. Both
    /// request modes collapse into it (on-demand → <c>now</c>..<c>now + duration</c>, scheduled → the chosen
    /// start/end), so there is no separate duration or mode field; the length is <see cref="LeaseNotAfter"/> minus
    /// <see cref="LeaseNotBefore"/>. In v1 the approved and leased windows are identical to this one.
    /// </summary>
    public DateTime LeaseNotBefore { get; }
    public DateTime LeaseNotAfter { get; }
    public string? Reason { get; }
    public DateTime SubmittedAt { get; }
    public DateTime? ResolvedAt { get; }

    /// <summary>Distinct from <see cref="ResolvedAt"/>; set when an approved request lapses unactivated. Not tracked in v1.</summary>
    public DateTime? ExpiredAt => null;

    /// <summary>
    /// The request's decision log, oldest first — one element per decision (human or automatic). Each carries who
    /// decided (<c>deciderKind</c>), the verdict, and (for a human decision) the approver's identity and comment.
    /// Empty only while pending. An array so multi-party approval lands without breaking the contract.
    /// </summary>
    public IEnumerable<AccessRequestDecisionResponseModel> Decisions { get; }

    /// <summary>Set once an approved request has produced a lease.</summary>
    public Guid? ProducedLeaseId { get; }

    /// <summary>
    /// The produced lease's status (<c>active | expired | revoked</c>), or null when no lease exists. The inbox uses
    /// this to keep an ended lease out of the "active" group so it is not offered for revocation.
    /// </summary>
    public string? ProducedLeaseStatus { get; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; }

    public string? RequesterName { get; }
    public string? RequesterEmail { get; }
}
