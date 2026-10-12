using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One offer of rotation work for a <see cref="PamRotationConfig"/>. A config has at most one unresolved job whose
/// timeout is not yet recorded (<c>AtMostOneActiveJobPerConfig</c>). A timeout writes nothing, so a timed-out claim
/// keeps its claim fields.
/// </summary>
public class PamRotationJob : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid RotationConfigId { get; set; }

    public PamRotationSource Source { get; set; }

    /// <summary>
    /// Who holds the job, or how it ended; <see cref="PamRotationStatusDerivation.ComputeJobStatus"/> derives the
    /// status from it against <see cref="ExpiresAt"/>. Doubles as the concurrency token for the guarded UPDATEs.
    /// </summary>
    public PamRotationJobAction Action { get; set; }

    /// <summary>The access connector holding this job's claim. Null unless <see cref="Action"/> is
    /// <see cref="PamRotationJobAction.Claimed"/>.</summary>
    public Guid? ClaimedByAccessConnectorId { get; set; }

    /// <summary>When the current claim was taken, and so its attempt's <see cref="PamRotationAttempt.CreationDate"/>.
    /// Null unless <see cref="Action"/> is <see cref="PamRotationJobAction.Claimed"/>.</summary>
    public DateTime? ClaimedAt { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>Pushed out by a retry's exponential backoff or by a release.</summary>
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
