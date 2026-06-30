using Bit.HttpExtensions;
using Bit.Pam.Entities;

namespace Bit.Commercial.Pam.Api.Models.Response;

/// <summary>
/// An access lease as its requester sees it: the originating request, string status vocabulary, and revocation
/// fields. Powers the request-submission envelope, the caller-scoped "my active leases" surface, and the cipher
/// access-state snapshot. Fields without a backing store in v1 (<see cref="RuleId"/>,
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
        Status = AccessLeaseStatusNames.From(lease.Status);
        NotBefore = lease.NotBefore.AsUtc();
        NotAfter = lease.NotAfter.AsUtc();
        RevokedAt = lease.RevokedDate.AsUtc();
        RevokedByUserId = lease.RevokedBy;
    }

    public Guid Id { get; }

    /// <summary>The request this lease was born from.</summary>
    public Guid RequestId { get; }

    public Guid CipherId { get; }
    public Guid CollectionId { get; }

    /// <summary>The access rule that gated the cipher at grant time. Not tracked in v1.</summary>
    public string? RuleId => null;

    public Guid OrganizationId { get; }

    /// <summary>The user the lease was granted to (the original requester).</summary>
    public Guid RequesterId { get; }

    /// <summary><c>active | expired | revoked</c>.</summary>
    public string Status { get; }

    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public DateTime? RevokedAt { get; }
    public Guid? RevokedByUserId { get; }

    /// <summary>The reason captured on early revocation. Recorded on the audit decision, not surfaced here in v1.</summary>
    public string? RevocationReason => null;
}
