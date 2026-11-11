using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>An access request with its denormalized requester identity.</summary>
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

    public Guid Id { get; set; }

    public Guid CipherId { get; set; }

    /// <summary>The collection through which the request is governed.</summary>
    public Guid CollectionId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid RequesterId { get; set; }

    /// <summary>The access rule pinned on the request at submit, if any.</summary>
    public Guid? RuleId { get; set; }

    /// <summary>
    /// The request's lifecycle state as of the read clock. An expired request whose <see cref="Decisions"/> hold an
    /// approval was approved but never activated.
    /// </summary>
    public AccessRequestStatus Status { get; set; }

    /// <summary>
    /// Start of the window resolved at submit (now to now + duration on the automatic path, the chosen start and end
    /// on the human path). The request can be activated only inside it.
    /// </summary>
    public DateTime LeaseNotBefore { get; set; }

    /// <summary>End of the window; see <see cref="LeaseNotBefore"/>.</summary>
    public DateTime LeaseNotAfter { get; set; }

    public string? Reason { get; set; }

    public DateTime SubmittedAt { get; set; }

    /// <summary>
    /// When the request was last approved, denied or cancelled. Null while pending or after expiring unanswered.
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>The request's decisions, oldest first.</summary>
    public IEnumerable<AccessRequestDecisionResponseModel> Decisions { get; set; } = null!;

    /// <summary>Set once an approved request has produced a lease.</summary>
    public Guid? ProducedLeaseId { get; set; }

    public AccessLeaseStatus? ProducedLeaseStatus { get; set; }

    /// <summary>The produced lease's end, including any extension, or null when no lease exists.</summary>
    public DateTime? ProducedLeaseNotAfter { get; set; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; set; }

    /// <summary>The requester's display name; null when unset or the user could not be resolved.</summary>
    public string? RequesterName { get; set; }

    /// <summary>The requester's email; null when the user could not be resolved.</summary>
    public string? RequesterEmail { get; set; }
}
