using Bit.Core.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Pam.Entities;

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

    public AccessLeaseStatus Status { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }

    public DateTime? RevokedDate { get; set; }
    public Guid? RevokedBy { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
