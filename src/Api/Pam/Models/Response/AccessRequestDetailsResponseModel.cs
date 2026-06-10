using Bit.Core.Models.Api;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// An access request with its denormalized display fields (cipher/collection names, requester identity), serving the
/// approver inbox, the caller's own request list, and the cipher access-state snapshot. Fields without a backing
/// store in v1 (<see cref="RuleId"/>, <see cref="ExpiredAt"/>, <see cref="ActivationDeadline"/>) are always null.
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
        Status = AccessRequestStatusNames.From(details.Status, details.ProducedLeaseId.HasValue);
        RequestedNotBefore = details.NotBefore;
        RequestedNotAfter = details.NotAfter;
        RequestedTtlSeconds = (int)(details.NotAfter - details.NotBefore).TotalSeconds;
        Reason = details.Reason;
        SubmittedAt = details.CreationDate;
        ResolvedAt = details.ResolvedDate;
        ApproverId = details.ApproverId;
        ApproverComment = details.ApproverComment;
        ProducedLeaseId = details.ProducedLeaseId;
        ExtensionOfLeaseId = details.ExtensionOfLeaseId;
        CipherName = details.CipherName;
        CollectionName = details.CollectionName;
        RequesterName = details.RequesterName;
        RequesterEmail = details.RequesterEmail;
    }

    public Guid Id { get; }
    public Guid CipherId { get; }
    public Guid CollectionId { get; }

    /// <summary>The access rule that gated the cipher at submit time. Not tracked in v1.</summary>
    public string? RuleId => null;

    public Guid OrganizationId { get; }
    public Guid RequesterId { get; }

    /// <summary><c>pending | approved | activated | denied | cancelled | expired</c>.</summary>
    public string Status { get; }

    public DateTime RequestedNotBefore { get; }
    public DateTime RequestedNotAfter { get; }
    public int RequestedTtlSeconds { get; }
    public string? Reason { get; }
    public DateTime SubmittedAt { get; }
    public DateTime? ResolvedAt { get; }

    /// <summary>Distinct from <see cref="ResolvedAt"/>; set when an approved request lapses unactivated. Not tracked in v1.</summary>
    public DateTime? ExpiredAt => null;

    /// <summary>The human approver who decided the request, or null (e.g. still pending or decided automatically).</summary>
    public Guid? ApproverId { get; }

    public string? ApproverComment { get; }

    /// <summary>Set once an approved request has produced a lease.</summary>
    public Guid? ProducedLeaseId { get; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; }

    /// <summary>Only meaningful for approved on-demand requests. Belongs to the out-of-scope activation flow.</summary>
    public DateTime? ActivationDeadline => null;

    /// <summary>The cipher's client-encrypted name. The only cipher attribute exposed by the inbox.</summary>
    public string? CipherName { get; }

    public string? CollectionName { get; }
    public string? RequesterName { get; }
    public string? RequesterEmail { get; }
}
