using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamRotationJob"/> with its attempts in scope for the read, oldest first. A config read includes every
/// attempt; an access connector read includes only the ones that access connector recorded.
/// </summary>
public class PamRotationJobDetails : PamRotationJob
{
    /// <summary>
    /// Derived from <see cref="PamRotationJob.Action"/> against the read's clock by
    /// <see cref="PamRotationStatusDerivation.ComputeJobStatus"/>.
    /// </summary>
    public PamRotationJobStatus Status { get; set; }

    public IReadOnlyList<PamRotationAttemptDetails> Attempts { get; set; } = [];

    /// <summary>
    /// Derives every status against <paramref name="now"/>. A timed-out claim keeps its claim fields in storage, but
    /// they are only surfaced while the job derives as Claimed.
    /// </summary>
    public static PamRotationJobDetails From(PamRotationJob job, IReadOnlyList<PamRotationAttempt> attempts,
        DateTime now)
    {
        var status = PamRotationStatusDerivation.ComputeJobStatus(job.Action, job.ExpiresAt, now);
        var claimed = status == PamRotationJobStatus.Claimed;

        return new PamRotationJobDetails
        {
            Id = job.Id,
            RotationConfigId = job.RotationConfigId,
            Source = job.Source,
            Action = job.Action,
            Status = status,
            ClaimedByAccessConnectorId = claimed ? job.ClaimedByAccessConnectorId : null,
            ClaimedAt = claimed ? job.ClaimedAt : null,
            CreationDate = job.CreationDate,
            NextClaimableAt = job.NextClaimableAt,
            ExpiresAt = job.ExpiresAt,
            Attempts = attempts.Select(attempt => PamRotationAttemptDetails.From(attempt, job, status)).ToList(),
        };
    }
}
