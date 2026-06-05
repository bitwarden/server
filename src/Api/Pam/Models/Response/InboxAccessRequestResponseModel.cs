using Bit.Core.Models.Api;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// An approver-inbox row: the access request plus the denormalized display fields the client renders. Matches the
/// client's <c>InboxAccessRequestResponse</c> shape. Fields without a backing store in v1 (<see cref="RuleId"/>,
/// <see cref="ExpiredAt"/>, <see cref="RedemptionDeadline"/>) are always null.
/// </summary>
public class InboxAccessRequestResponseModel : ResponseModel
{
    public InboxAccessRequestResponseModel(InboxLeaseRequestDetails details)
        : base("inboxAccessRequest")
    {
        ArgumentNullException.ThrowIfNull(details);

        Id = details.Id;
        CipherId = details.CipherId;
        CollectionId = details.CollectionId;
        OrganizationId = details.OrganizationId;
        RequesterUserId = details.RequesterId;
        Status = InboxRequestStatus.From(details.Status, details.ProducedLeaseId.HasValue);
        RequestedNotBefore = details.NotBefore;
        RequestedNotAfter = details.NotAfter;
        RequestedTtlSeconds = (int)(details.NotAfter - details.NotBefore).TotalSeconds;
        Reason = details.Reason;
        SubmittedAt = details.CreationDate;
        ResolvedAt = details.ResolvedDate;
        ResolverUserId = details.ResolverId;
        ResolverComment = details.ResolverComment;
        LeaseId = details.ProducedLeaseId;
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
    public Guid RequesterUserId { get; }

    /// <summary><c>pending | approved | activated | denied | cancelled | expired</c>.</summary>
    public string Status { get; }

    public DateTime RequestedNotBefore { get; }
    public DateTime RequestedNotAfter { get; }
    public int RequestedTtlSeconds { get; }
    public string? Reason { get; }
    public DateTime SubmittedAt { get; }
    public DateTime? ResolvedAt { get; }

    /// <summary>Distinct from <see cref="ResolvedAt"/>; set when a ticket lapses. Not tracked in v1.</summary>
    public DateTime? ExpiredAt => null;

    public Guid? ResolverUserId { get; }
    public string? ResolverComment { get; }

    /// <summary>Set once an approved ticket has produced a lease.</summary>
    public Guid? LeaseId { get; }

    /// <summary>The parent lease if this is an extension request.</summary>
    public Guid? ExtensionOfLeaseId { get; }

    /// <summary>Only meaningful for approved on-demand tickets. Belongs to the out-of-scope redemption flow.</summary>
    public DateTime? RedemptionDeadline => null;

    /// <summary>The cipher's client-encrypted name. The only cipher attribute exposed by the inbox.</summary>
    public string? CipherName { get; }

    public string? CollectionName { get; }
    public string? RequesterName { get; }
    public string? RequesterEmail { get; }
}
