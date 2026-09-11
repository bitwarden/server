using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// A grant of access to a cipher, born from an approved <see cref="AccessRequest"/>. Only a lease with no early end
/// recorded (<see cref="Action"/> <see cref="AccessLeaseAction.None"/>) inside its <see cref="NotBefore"/>/
/// <see cref="NotAfter"/> window authorizes access.
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
    /// How the lease was ended <em>early</em>, and nothing else; a lease that lapses untouched carries
    /// <see cref="AccessLeaseAction.None"/> forever. The wire's <see cref="AccessLeaseStatus"/> is derived from this
    /// plus the clock via <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>.
    /// </summary>
    public AccessLeaseAction Action { get; set; }

    /// <summary>
    /// The start of the granted access window, carried over from the approved <see cref="AccessRequest"/>. In the
    /// past from the moment the row exists — activation rejects a future start and the mint procedure re-guards it —
    /// so status derivation may ignore it (see <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>).
    /// </summary>
    public DateTime NotBefore { get; set; }

    /// <summary>
    /// The end of the granted access window.
    /// </summary>
    public DateTime NotAfter { get; set; }

    /// <summary>
    /// When the lease was ended early, for <see cref="AccessLeaseAction.Revoked"/> or
    /// <see cref="AccessLeaseAction.Cancelled"/>. NULL otherwise.
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// Who ended the lease early: the operator who revoked it, or the holder who cancelled their own. NULL
    /// outside <see cref="AccessLeaseAction.Revoked"/>/<see cref="AccessLeaseAction.Cancelled"/>.
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// When the lease was minted, stamped in UTC at construction.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the lease authorizes access as of <paramref name="asOf"/>: no early end recorded and the window
    /// still open. The single liveness question every write guard consults.
    /// </summary>
    public bool IsLive(DateTime asOf) =>
        AccessStatusDerivation.ComputeLeaseStatus(Action, NotAfter, asOf) == AccessLeaseStatus.Active;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
