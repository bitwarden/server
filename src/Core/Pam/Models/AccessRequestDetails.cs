using Bit.Core.Pam.Enums;

namespace Bit.Core.Pam.Models;

/// <summary>
/// A lease request projected for the approver inbox: every <see cref="Entities.AccessRequest"/> field plus the
/// denormalized display data the client needs (cipher/collection names, requester identity), the lease the request
/// produced (if any), and the human resolver's identity/comment. Populated by a single join in the read procedures so
/// the client avoids an N+1.
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
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public string? Reason { get; set; }
    public AccessRequestStatus Status { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime? ResolvedDate { get; set; }

    /// <summary>The lease this request produced once activated, or null if it has not produced a lease.</summary>
    public Guid? ProducedLeaseId { get; set; }

    /// <summary>
    /// The produced lease's current status (Active/Expired/Revoked), or null when the request has not produced a
    /// lease. Lets the inbox distinguish a still-live lease from one that has ended, so an ended lease is not offered
    /// for revocation.
    /// </summary>
    public AccessLeaseStatus? ProducedLeaseStatus { get; set; }

    /// <summary>The human approver who resolved the request, or null (e.g. still pending or auto-resolved).</summary>
    public Guid? ApproverId { get; set; }

    /// <summary>
    /// The human approver's display name, denormalized from the User join so the requester's own
    /// request list can name the resolver instead of showing a raw id. Null when no human resolved.
    /// </summary>
    public string? ApproverName { get; set; }

    /// <summary>The human approver's email, the fallback display when <see cref="ApproverName"/> is unset.</summary>
    public string? ApproverEmail { get; set; }

    /// <summary>The human approver's comment, if any.</summary>
    public string? ApproverComment { get; set; }

    /// <summary>The cipher's client-encrypted name. The only cipher attribute the inbox exposes.</summary>
    public string? CipherName { get; set; }

    public string? CollectionName { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
}
