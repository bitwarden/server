using Bit.HttpExtensions;

namespace Bit.Commercial.Pam.Api.Models.Response;

/// <summary>
/// An access lease as its requester sees it: the originating request, string status vocabulary, and revocation
/// fields. Powers the request-submission envelope, the caller-scoped "my active leases" surface, and the cipher
/// access-state snapshot. Fields without a backing store in v1 (<see cref="RuleId"/>,
/// <see cref="RevocationReason"/>) are null.
/// </summary>
public class AccessLeaseResponseModel : ResponseModel
{
    public AccessLeaseResponseModel()
        : base("accessLease")
    {
    }

    /// <summary>The lease's unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The request this lease was born from.</summary>
    public Guid RequestId { get; set; }

    /// <summary>The cipher the lease grants access to.</summary>
    public Guid CipherId { get; set; }

    /// <summary>The collection the cipher belongs to.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>The access rule that gated the cipher at grant time. Not tracked in v1.</summary>
    public string? RuleId { get; set; }

    /// <summary>The organization that owns the cipher.</summary>
    public Guid OrganizationId { get; set; }

    /// <summary>The user the lease was granted to (the original requester).</summary>
    public Guid RequesterId { get; set; }

    /// <summary><c>active | expired | revoked</c>.</summary>
    public string Status { get; set; } = null!;

    /// <summary>When the lease's access window opens (UTC).</summary>
    public DateTime NotBefore { get; set; }

    /// <summary>When the lease's access window closes (UTC).</summary>
    public DateTime NotAfter { get; set; }

    /// <summary>When the lease was revoked early (UTC); null unless it was revoked before expiry.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>The user who revoked the lease; null unless it was revoked early.</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>The reason captured on early revocation. Recorded on the audit decision, not surfaced here in v1.</summary>
    public string? RevocationReason { get; set; }
}
