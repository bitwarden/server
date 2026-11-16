using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>A grant of access to a cipher, minted when its approved <see cref="AccessRequest"/> is activated.</summary>
public class AccessLease : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid AccessRequestId { get; set; }

    public Guid OrganizationId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid CipherId { get; set; }
    public Guid RequesterId { get; set; }

    /// <summary>
    /// How the lease was ended early; a lease that lapses untouched keeps <see cref="AccessLeaseAction.None"/>.
    /// </summary>
    public AccessLeaseAction Action { get; set; }

    /// <summary>
    /// The activation time, never backdated to the request's start, so it is always past once the row exists.
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// Starts as the request's end, so a late activation shortens the lease; an extension pushes it out.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>Set when the lease is revoked or cancelled.</summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>The operator who revoked the lease, or the holder who cancelled it.</summary>
    public Guid? RevokedBy { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>Whether the lease authorizes access as of <paramref name="asOf"/>.</summary>
    public bool IsLive(DateTime asOf) =>
        AccessStatusDerivation.ComputeLeaseStatus(Action, NotAfter, asOf) == AccessLeaseStatus.Active;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
