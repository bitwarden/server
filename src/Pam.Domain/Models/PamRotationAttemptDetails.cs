using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamRotationAttempt"/> with its status derived against its job — an element of
/// <see cref="PamRotationJobDetails.Attempts"/>.
/// </summary>
public class PamRotationAttemptDetails : PamRotationAttempt
{
    /// <summary>
    /// Derived from <see cref="PamRotationAttempt.Action"/> by
    /// <see cref="PamRotationStatusDerivation.ComputeAttemptStatus"/>.
    /// </summary>
    public PamRotationAttemptStatus Status { get; set; }

    /// <param name="job">The attempt's job as stored, whose claim fields decide whether this is the current
    /// attempt.</param>
    /// <param name="jobStatus">The job's derived status.</param>
    public static PamRotationAttemptDetails From(PamRotationAttempt attempt, PamRotationJob job,
        PamRotationJobStatus jobStatus)
    {
        var status = PamRotationStatusDerivation.ComputeAttemptStatus(
            attempt.Action, PamRotationRules.IsCurrentAttempt(job, attempt), jobStatus);

        return new PamRotationAttemptDetails
        {
            Id = attempt.Id,
            JobId = attempt.JobId,
            ClaimedByAccessConnectorId = attempt.ClaimedByAccessConnectorId,
            CipherUpdated = attempt.CipherUpdated,
            Action = attempt.Action,
            Status = status,
            FailureReason = attempt.FailureReason,
            SyncState = attempt.SyncState,
            SessionTermination = attempt.SessionTermination,
            CreationDate = attempt.CreationDate,
            // A timeout writes nothing, so an attempt it abandoned ended at its job's deadline.
            ResolvedDate = attempt.ResolvedDate
                ?? (status == PamRotationAttemptStatus.Abandoned && jobStatus == PamRotationJobStatus.TimedOut
                    ? job.ExpiresAt
                    : null),
        };
    }
}
