using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// The result of recording a failed attempt. On <see cref="PamRotationAttemptResolveOutcome.Resolved"/>,
/// <see cref="JobStatus"/> is Pending for a retry, or Failed once the retry budget is exhausted.
/// </summary>
public class PamRotationFailureResult
{
    public required PamRotationAttemptResolveOutcome Outcome { get; init; }

    /// <summary>Null for <see cref="PamRotationAttemptResolveOutcome.Rejected"/>.</summary>
    public PamRotationJobStatus? JobStatus { get; init; }

    /// <summary>Errored attempts on the job including this one, as checked against <c>MaxAttempts</c>.</summary>
    public int ErroredAttemptCount { get; init; }
}
