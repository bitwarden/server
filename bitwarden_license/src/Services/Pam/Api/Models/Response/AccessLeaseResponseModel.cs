using Bit.HttpExtensions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Api.Models.Response;

/// <summary>An access lease, with <see cref="Status"/> derived against the read clock.</summary>
public class AccessLeaseResponseModel : ResponseModel
{
    public AccessLeaseResponseModel()
        : base("accessLease")
    {
    }

    public AccessLeaseResponseModel(AccessLease lease, DateTime asOf)
        : base("accessLease")
    {
        ArgumentNullException.ThrowIfNull(lease);

        Id = lease.Id;
        RequestId = lease.AccessRequestId;
        CipherId = lease.CipherId;
        CollectionId = lease.CollectionId;
        OrganizationId = lease.OrganizationId;
        RequesterId = lease.RequesterId;
        Status = AccessStatusDerivation.ComputeLeaseStatus(lease.Action, lease.NotAfter, asOf);
        NotBefore = lease.NotBefore.AsUtc();
        NotAfter = lease.NotAfter.AsUtc();
        RevokedAt = lease.RevokedDate.AsUtc();
        RevokedByUserId = lease.RevokedBy;
    }

    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid CipherId { get; set; }

    /// <summary>The collection through which the lease was granted.</summary>
    public Guid CollectionId { get; set; }

    /// <summary>Always null; leases do not record their rule.</summary>
    public string? RuleId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid RequesterId { get; set; }

    public AccessLeaseStatus Status { get; set; }

    public DateTime NotBefore { get; set; }

    public DateTime NotAfter { get; set; }

    /// <summary>When the lease was revoked or cancelled early; otherwise null.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Who revoked or cancelled the lease early; otherwise null.</summary>
    public Guid? RevokedByUserId { get; set; }

    /// <summary>Always null; the reason is recorded as a decision on the originating request.</summary>
    public string? RevocationReason { get; set; }
}
