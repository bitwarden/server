using Bit.Core.Models.Api;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Models;

namespace Bit.Api.Pam.Models.Response;

/// <summary>
/// A lease as its grantee sees it. Matches the client's <c>LeaseResponse</c> shape — a richer view than the minimal
/// <see cref="LeaseResponseModel"/> returned by the request flow, with the originating request, grantee, string status
/// vocabulary, and revocation fields. Powers the caller-scoped "my active leases" surface and the cipher-lease-state
/// snapshot. Fields without a backing store in v1 (<see cref="RuleId"/>, <see cref="RevocationReason"/>) are null.
/// </summary>
public class MemberLeaseResponseModel : ResponseModel
{
    public MemberLeaseResponseModel(Lease lease)
        : base("lease")
    {
        ArgumentNullException.ThrowIfNull(lease);

        Id = lease.Id;
        RequestId = lease.LeaseRequestId;
        CipherId = lease.CipherId;
        CollectionId = lease.CollectionId;
        OrganizationId = lease.OrganizationId;
        GranteeUserId = lease.RequesterId;
        Status = LeaseStatusName.From(lease.Status);
        NotBefore = lease.NotBefore;
        NotAfter = lease.NotAfter;
        RevokedAt = lease.RevokedDate;
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
    public Guid GranteeUserId { get; }

    /// <summary><c>active | expired | revoked</c>.</summary>
    public string Status { get; }

    public DateTime NotBefore { get; }
    public DateTime NotAfter { get; }
    public DateTime? RevokedAt { get; }
    public Guid? RevokedByUserId { get; }

    /// <summary>The reason captured on early revocation. Recorded on the audit decision, not surfaced here in v1.</summary>
    public string? RevocationReason => null;
}
