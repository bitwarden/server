using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// An active grant of access to a cipher, born from an approved <see cref="AccessRequest"/>. Only
/// <see cref="AccessLeaseStatus.Active"/> leases inside their <see cref="NotBefore"/>/<see cref="NotAfter"/> window
/// authorize access.
/// </summary>
public class AccessLease : ITableObject<Guid>
{
    public Guid Id { get; set; }

    /// <summary>
    /// The request that birthed this lease.
    /// </summary>
    public Guid AccessRequestId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>
    /// The lease's position in its lifecycle. Only an <see cref="AccessLeaseStatus.Active"/> lease within its window
    /// authorizes access.
    /// </summary>
    public AccessLeaseStatus Status { get; set; }

    /// <summary>
    /// The start of the granted access window, carried over from the approved <see cref="AccessRequest"/>.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// The end of the granted access window.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// Set when an operator revokes the lease (<see cref="AccessLeaseStatus.Revoked"/>). NULL otherwise.
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// The operator who revoked the lease. NULL unless <see cref="Status"/> is <see cref="AccessLeaseStatus.Revoked"/>.
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// When the lease was minted, stamped in UTC at construction.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
