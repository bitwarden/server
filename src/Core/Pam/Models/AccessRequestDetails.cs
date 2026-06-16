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

    /// <summary>
    /// Every decision recorded against this request, oldest first — one element per
    /// <see cref="Entities.AccessDecision"/> row (human or automatic; identity denormalized from the User join for
    /// human decisions). Empty only while pending (no decision recorded yet). The resolved reads return the decisions
    /// as a second result set that the repository groups onto this list; the constructed reads (decision result,
    /// cipher access-state snapshot) set it directly.
    /// </summary>
    public List<AccessRequestDecision> Decisions { get; set; } = new();

    /// <summary>The cipher's client-encrypted name. The only cipher attribute the inbox exposes.</summary>
    public string? CipherName { get; set; }

    public string? CollectionName { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }
}
