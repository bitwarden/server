using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One access connector's try at executing a <see cref="PamRotationJob"/>. Invariant
/// <c>AtMostOneInFlightAttemptPerJob</c> — a job has at most one <see cref="PamRotationAttemptStatus.Executing"/>
/// attempt at a time, inserted atomically with the claim that creates it. Reaching
/// <see cref="PamRotationAttemptStatus.Rotated"/> requires both a written cipher (<see cref="CipherUpdated"/>) and a
/// claimant-verified success report — the <c>VerifiedBeforeSuccess</c> backstop.
/// </summary>
public class PamRotationAttempt : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    /// <summary>The access connector executing this attempt, fixed for its lifetime (unlike the job's claim fields, this is never cleared).</summary>
    public Guid ClaimedByAccessConnectorId { get; set; }

    /// <summary>Whether the access connector has written the rotated secret back to the cipher via the atomic accept-write path.</summary>
    public bool CipherUpdated { get; set; }

    /// <summary>
    /// What the claimant reported, if anything. Executing vs Abandoned is derived via
    /// <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/>.
    /// </summary>
    public PamRotationAttemptAction Action { get; set; }

    /// <summary>
    /// A bounded, human-readable failure reason, truncated to 500 characters server-side. Set only on
    /// <see cref="PamRotationAttemptStatus.Errored"/>; forwarding raw target-system error output is forbidden
    /// since it can echo credentials.
    /// </summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>Whether the target system's password was left changed by a failed attempt, set only on Errored.</summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>The outcome of the requested session termination, set only by a Rotated attempt that reports it.</summary>
    public PamSessionTerminationOutcome? SessionTermination { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the claimant reported, or when a release ended the claim. A timeout writes nothing, so a timed-out
    /// attempt has none stored.</summary>
    public DateTime? ResolvedDate { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
