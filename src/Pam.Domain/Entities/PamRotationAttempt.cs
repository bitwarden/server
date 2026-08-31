using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Utilities;
using Bit.Pam.Enums;

namespace Bit.Pam.Entities;

/// <summary>
/// One daemon's try at executing a <see cref="PamRotationJob"/>. Invariant <c>AtMostOneInFlightAttemptPerJob</c> — a
/// job has at most one <see cref="PamRotationAttemptStatus.Executing"/> attempt at a time, inserted atomically with
/// the claim that creates it. Reaching <see cref="PamRotationAttemptStatus.Rotated"/> requires both a written cipher
/// (<see cref="CipherUpdated"/>) and a claimant-verified success report — the <c>VerifiedBeforeSuccess</c> backstop.
/// </summary>
public class PamRotationAttempt : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    /// <summary>The daemon executing this attempt, fixed for its lifetime (unlike the job's claim fields, this is never cleared).</summary>
    public Guid ClaimedByDaemonId { get; set; }

    /// <summary>Whether the daemon has written the rotated secret back to the cipher via the atomic accept-write path.</summary>
    public bool CipherUpdated { get; set; }

    public PamRotationAttemptStatus Status { get; set; }

    /// <summary>
    /// A bounded, human-readable failure reason, truncated to 500 characters server-side (never rejected). The
    /// contract forbids forwarding raw target-system error output, since it can echo credentials. Null unless
    /// <see cref="Status"/> is <see cref="PamRotationAttemptStatus.Errored"/>.
    /// </summary>
    [MaxLength(500)]
    public string? FailureReason { get; set; }

    /// <summary>Whether the target system's password was left changed by a failed attempt. Null unless <see cref="Status"/> is Errored.</summary>
    public PamRotationSyncState? SyncState { get; set; }

    /// <summary>The outcome of the requested session termination, if any. Set only when a Rotated attempt reports it.</summary>
    public PamSessionTerminationOutcome? SessionTermination { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>When the attempt left <see cref="PamRotationAttemptStatus.Executing"/>. Null while still executing.</summary>
    public DateTime? ResolvedDate { get; set; }

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
