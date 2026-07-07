using Bit.HttpExtensions;
using Bit.Pam.Entities;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>
/// An access lease as its requester sees it: the originating request, its lifecycle <see cref="Status"/>, and
/// revocation fields. Powers the request-submission envelope, the caller-scoped "my active leases" surface, and the
/// cipher access-state snapshot. Fields without a backing store in v1 (<see cref="RuleId"/>,
/// <see cref="RevocationReason"/>) are null.
/// </summary>
public class AccessLeaseResponseModel : ResponseModel
{
    public AccessLeaseResponseModel(AccessLease lease)
        : base("accessLease")
    {
        ArgumentNullException.ThrowIfNull(lease);

        Id = lease.Id;
        RequestId = lease.AccessRequestId;
        CipherId = lease.CipherId;
        CollectionId = lease.CollectionId;
        OrganizationId = lease.OrganizationId;
        RequesterId = lease.RequesterId;
        Status = lease.Status.ToApiStatus();
        NotBefore = lease.NotBefore.AsUtc();
        NotAfter = lease.NotAfter.AsUtc();
        RevokedAt = lease.RevokedDate.AsUtc();
        RevokedByUserId = lease.RevokedBy;
    }

    /// <summary>The lease's unique identifier.</summary>
    public Guid Id { get; }

    /// <summary>The request this lease was born from.</summary>
    public Guid RequestId { get; }

    /// <summary>The cipher the lease grants access to.</summary>
    public Guid CipherId { get; }

    /// <summary>The collection the cipher belongs to.</summary>
    public Guid CollectionId { get; }

    /// <summary>The access rule that gated the cipher at grant time. Not tracked in v1.</summary>
    public string? RuleId => null;

    /// <summary>The organization that owns the cipher.</summary>
    public Guid OrganizationId { get; }

    /// <summary>The user the lease was granted to (the original requester).</summary>
    public Guid RequesterId { get; }

    /// <summary>The lease's lifecycle state.</summary>
    public AccessLeaseStatus Status { get; }

    /// <summary>When the lease's access window opens (UTC).</summary>
    public DateTime NotBefore { get; }

    /// <summary>When the lease's access window closes (UTC).</summary>
    public DateTime NotAfter { get; }

    /// <summary>When the lease was revoked early (UTC); null unless it was revoked before expiry.</summary>
    public DateTime? RevokedAt { get; }

    /// <summary>The user who revoked the lease; null unless it was revoked early.</summary>
    public Guid? RevokedByUserId { get; }

    /// <summary>The reason captured on early revocation. Recorded on the audit decision, not surfaced here in v1.</summary>
    public string? RevocationReason => null;
}
