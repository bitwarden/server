using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The result of <c>IPamRotationJobRepository.MarkAttemptErroredAsync</c>. On
/// <see cref="PamRotationAttemptResolveOutcome.Resolved"/>, <see cref="JobStatus"/> reports whether the job was
/// retried (<see cref="Enums.PamRotationJobStatus.Pending"/>, retry budget remaining) or failed outright
/// (<see cref="Enums.PamRotationJobStatus.Failed"/>, retry budget exhausted).
/// </summary>
public class PamRotationFailureResult
{
    public required PamRotationAttemptResolveOutcome Outcome { get; init; }

    /// <summary>Null when <see cref="Outcome"/> is <see cref="PamRotationAttemptResolveOutcome.Rejected"/> (a stale report).</summary>
    public PamRotationJobStatus? JobStatus { get; init; }

    /// <summary>The number of Errored attempts recorded against the job, including this one — checked against <c>MaxAttempts</c>.</summary>
    public int ErroredAttemptCount { get; init; }
}
