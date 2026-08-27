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
    /// The action a party has taken on the lease, if any — how it was ended <em>early</em>, and nothing else. The
    /// happy-path lease is minted, used, and lapses untouched, carrying <see cref="AccessLeaseAction.None"/> forever;
    /// what that means right now (the wire's <see cref="AccessLeaseStatus"/>) is derived against the clock at read
    /// time via <see cref="AccessStatusDerivation.ComputeLeaseStatus"/>, which is where Active and Expired come from.
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
    /// When the lease was ended early — set for both <see cref="AccessLeaseAction.Revoked"/> (an operator ended it)
    /// and <see cref="AccessLeaseAction.Cancelled"/> (the holder ended their own). NULL otherwise.
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// Who ended the lease early: the operator who revoked it, or the holder who cancelled their own. NULL unless
    /// <see cref="Action"/> is <see cref="AccessLeaseAction.Revoked"/> or <see cref="AccessLeaseAction.Cancelled"/>.
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// When the lease was minted, stamped in UTC at construction.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the lease authorizes access as of <paramref name="asOf"/>: no early end recorded and the window still
    /// open. The write guards' single liveness question (may this lease be ended, extended, or returned as an
    /// activation winner?), anchored to the one status producer so a new lease state cannot silently diverge the
    /// write path — this is the only place a write guard consults the derivation, and it collapses it to a bool.
    /// </summary>
    public bool IsLive(DateTime asOf) =>
        AccessStatusDerivation.ComputeLeaseStatus(Action, NotAfter, asOf) == AccessLeaseStatus.Active;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
