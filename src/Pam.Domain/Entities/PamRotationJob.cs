using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One offer of rotation work for a <see cref="PamRotationConfig"/>. Invariant <c>AtMostOneActiveJobPerConfig</c> —
/// a config has at most one <see cref="PamRotationJobStatus.Pending"/> or <see cref="PamRotationJobStatus.Claimed"/>
/// job at a time; <c>OfferRotationCommand</c> is the single creation point. Every transition out of
/// <see cref="PamRotationJobStatus.Claimed"/> — retry, release, success, or timeout — clears
/// <see cref="ClaimedByDaemonId"/> and <see cref="ClaimedAt"/>; the executing daemon's history lives on the
/// <see cref="PamRotationAttempt"/> instead.
/// </summary>
public class PamRotationJob : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid RotationConfigId { get; set; }

    public PamRotationSource Source { get; set; }

    public PamRotationJobStatus Status { get; set; }

    /// <summary>The daemon currently holding this job's claim. Null unless <see cref="Status"/> is <see cref="PamRotationJobStatus.Claimed"/>.</summary>
    public Guid? ClaimedByDaemonId { get; set; }

    /// <summary>When the current claim was taken. Null unless <see cref="Status"/> is <see cref="PamRotationJobStatus.Claimed"/>.</summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>The earliest time this job can be claimed — pushed out on retry (exponential backoff) or release.</summary>
    public DateTime NextClaimableAt { get; set; }

    /// <summary>
    /// <c>CreationDate + JobTtl</c>, persisted at creation. Once past this point with the job still Pending or
    /// Claimed and no <see cref="PamRotationAttemptStatus.Rotated"/> attempt, the sweep times the job out (spec
    /// <c>JobTimesOut</c>).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
