namespace Bit.Pam.Enums;

/// <summary>
/// The rotation counterpart to <see cref="AccessStatusDerivation"/>: job and attempt statuses are computed here at
/// read time from what was recorded, against the same deadline comparison the timeout sweep uses.
/// </summary>
public static class PamRotationStatusDerivation
{
    /// <summary>
    /// A job's status derived from its stored <see cref="PamRotationJobAction"/>. A recorded outcome beats the clock;
    /// an unresolved job, claimed or not, is timed out from <paramref name="expiresAt"/> on.
    /// </summary>
    public static PamRotationJobStatus ComputeJobStatus(PamRotationJobAction action, DateTime expiresAt, DateTime now)
    {
        var timedOut = now >= expiresAt;

        return action switch
        {
            PamRotationJobAction.Succeeded => PamRotationJobStatus.Succeeded,
            PamRotationJobAction.Failed => PamRotationJobStatus.Failed,
            PamRotationJobAction.Claimed => timedOut ? PamRotationJobStatus.TimedOut : PamRotationJobStatus.Claimed,
            PamRotationJobAction.None => timedOut ? PamRotationJobStatus.TimedOut : PamRotationJobStatus.Pending,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    /// <summary>
    /// An attempt's status derived from its stored <see cref="PamRotationAttemptAction"/>. A report beats everything;
    /// an unreported attempt is Executing only while it holds its job's live claim, and Abandoned once that claim was
    /// released or timed out.
    /// </summary>
    /// <param name="isCurrentAttempt">Whether the claim the job records is the one that created this attempt; see
    /// <see cref="PamRotationRules.IsCurrentAttempt"/>.</param>
    /// <param name="jobStatus">The job's status from <see cref="ComputeJobStatus"/>.</param>
    public static PamRotationAttemptStatus ComputeAttemptStatus(
        PamRotationAttemptAction action, bool isCurrentAttempt, PamRotationJobStatus jobStatus) =>
        action switch
        {
            PamRotationAttemptAction.Rotated => PamRotationAttemptStatus.Rotated,
            PamRotationAttemptAction.Errored => PamRotationAttemptStatus.Errored,
            PamRotationAttemptAction.None => isCurrentAttempt && jobStatus == PamRotationJobStatus.Claimed
                ? PamRotationAttemptStatus.Executing
                : PamRotationAttemptStatus.Abandoned,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
