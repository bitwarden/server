namespace Bit.Pam.Enums;

/// <summary>
/// Who holds a <see cref="Entities.PamRotationJob"/>, or how it ended. Unlike <see cref="AccessRequestAction"/> this
/// moves backwards: a retry or a release returns <see cref="Claimed"/> to <see cref="None"/>, so it records current
/// custody rather than an append-only decision. It still serves as the concurrency token the guarded UPDATEs key off.
/// Current standing, TimedOut included, is derived by <see cref="PamRotationStatusDerivation.ComputeJobStatus"/>.
/// </summary>
public enum PamRotationJobAction : byte
{
    /// <summary>Unclaimed: newly offered, or returned by a retry or a release. Pending vs TimedOut is the clock's call
    /// at read time.</summary>
    None = 0,

    /// <summary>Held by <see cref="Entities.PamRotationJob.ClaimedByDaemonId"/>. Claimed vs TimedOut is the clock's
    /// call at read time.</summary>
    Claimed = 1,

    /// <summary>An attempt against this job reported success.</summary>
    Succeeded = 2,

    /// <summary>Every attempt errored and the retry budget (<c>MaxAttempts</c>) is exhausted.</summary>
    Failed = 3,
}
