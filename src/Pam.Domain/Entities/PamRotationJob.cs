using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One offer of rotation work for a <see cref="PamRotationConfig"/>. Invariant <c>AtMostOneActiveJobPerConfig</c>:
/// a config has at most one unresolved job whose timeout the sweep has not yet recorded. Every write that ends a claim
/// clears <see cref="ClaimedByAccessConnectorId"/> and <see cref="ClaimedAt"/>; a claim that times out keeps them,
/// since a timeout writes nothing. The executing access connector's history lives on <see cref="PamRotationAttempt"/>
/// instead.
/// </summary>
public class PamRotationJob : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid RotationConfigId { get; set; }

    public PamRotationSource Source { get; set; }

    /// <summary>
    /// Who holds the job, or how it ended — a record of what happened, not current standing; the wire's
    /// <see cref="PamRotationJobStatus"/> is derived from it against <see cref="ExpiresAt"/> via
    /// <see cref="PamRotationStatusDerivation.ComputeJobStatus"/>. Doubles as the concurrency token the transition
    /// procedures' guarded UPDATEs key off.
    /// </summary>
    public PamRotationJobAction Action { get; set; }

    /// <summary>The access connector holding this job's claim. Null unless <see cref="Action"/> is
    /// <see cref="PamRotationJobAction.Claimed"/>.</summary>
    public Guid? ClaimedByAccessConnectorId { get; set; }

    /// <summary>When the current claim was taken, and so its attempt's <see cref="PamRotationAttempt.CreationDate"/>.
    /// Null unless <see cref="Action"/> is <see cref="PamRotationJobAction.Claimed"/>.</summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>The earliest time this job can be claimed — pushed out on retry (exponential backoff) or release.</summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>
    /// <c>CreationDate + JobTtl</c>, persisted at creation. From this point on an unresolved job derives as
    /// <see cref="PamRotationJobStatus.TimedOut"/>; the sweep records the timeout once (spec <c>JobTimesOut</c>).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
