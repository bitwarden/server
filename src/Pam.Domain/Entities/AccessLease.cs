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
    /// The lease's position in its lifecycle, <em>as stored</em>. Only an <see cref="AccessLeaseStatus.Active"/>
    /// lease within its window authorizes access.
    /// </summary>
    /// <remarks>
    /// This column records how a lease was ended <em>early</em>, and nothing else. A lease whose window simply
    /// closed is never written back to <see cref="AccessLeaseStatus.Expired"/> — there is no sweeper — so a lapsed
    /// lease sits here as <see cref="AccessLeaseStatus.Active"/> forever. Never surface this value, or compare it
    /// against <see cref="AccessLeaseStatus.Active"/>, without a clock: use <see cref="StatusAsOf"/>. Reading it
    /// raw is what made the API report an ended lease as Active (PM-42355).
    /// </remarks>
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
    /// When the lease was ended early — set for both <see cref="AccessLeaseStatus.Revoked"/> (an operator ended it)
    /// and <see cref="AccessLeaseStatus.Cancelled"/> (the holder ended their own). NULL otherwise.
    /// </summary>
    public DateTime? RevokedDate { get; set; }

    /// <summary>
    /// Who ended the lease early: the operator who revoked it, or the holder who cancelled their own. NULL unless
    /// <see cref="Status"/> is <see cref="AccessLeaseStatus.Revoked"/> or <see cref="AccessLeaseStatus.Cancelled"/>.
    /// </summary>
    public Guid? RevokedBy { get; set; }

    /// <summary>
    /// When the lease was minted, stamped in UTC at construction.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The lease's status as of <paramref name="asOf"/> — <see cref="Status"/> projected against the clock, which is
    /// the only place <see cref="AccessLeaseStatus.Expired"/> comes from.
    /// </summary>
    /// <remarks>
    /// A stored <see cref="AccessLeaseStatus.Revoked"/> or <see cref="AccessLeaseStatus.Cancelled"/> wins over the
    /// window: a lease ended early ended early, whatever its <see cref="NotAfter"/> says. Only a stored
    /// <see cref="AccessLeaseStatus.Active"/> is reinterpreted, and only once its window has closed.
    ///
    /// The read procedures compute exactly this in SQL (so a read that filters on ended-ness can do it in the
    /// WHERE clause rather than over-fetching); this is the same rule for callers holding a materialized lease.
    /// The two must not drift.
    /// </remarks>
    public AccessLeaseStatus StatusAsOf(DateTime asOf) =>
        Status == AccessLeaseStatus.Active && NotAfter <= asOf
            ? AccessLeaseStatus.Expired
            : Status;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
