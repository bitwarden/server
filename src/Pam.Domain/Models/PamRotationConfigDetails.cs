using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam.Models;

public class PamRotationConfigDetails : PamRotationConfig
{
    public string TargetSystemName { get; set; } = null!;
    public PamTargetSystemMethod TargetSystemMethod { get; set; }

    /// <summary>
    /// Spec <c>has_active_job</c>: a job is unresolved and its timeout not yet recorded by the sweep. Can trail a job's
    /// derived TimedOut by one sweep, so the sweep's reschedule lands before the config is offered again.
    /// </summary>
    public bool HasActiveJob { get; set; }

    /// <summary>
    /// Whether the newest job ended badly: <see cref="PamRotationJobAction.Failed"/>, or unresolved with its timeout
    /// already recorded by the sweep. Lets a list read tell Failed apart without fetching each config's history.
    /// Mutually exclusive with <see cref="HasActiveJob"/>, which holds an unswept job active.
    /// </summary>
    public bool LastJobFailed { get; set; }

    public static PamRotationConfigDetails From(PamRotationConfig config, string targetSystemName,
        PamTargetSystemMethod targetSystemMethod, bool hasActiveJob, bool lastJobFailed = false) => new()
        {
            Id = config.Id,
            OrganizationId = config.OrganizationId,
            CipherId = config.CipherId,
            TargetSystemId = config.TargetSystemId,
            SealedData = config.SealedData,
            WrappedKey = config.WrappedKey,
            ScheduleCron = config.ScheduleCron,
            RotateOnAccessEnd = config.RotateOnAccessEnd,
            NextRotationDate = config.NextRotationDate,
            Enabled = config.Enabled,
            LastRotationDate = config.LastRotationDate,
            CreationDate = config.CreationDate,
            RevisionDate = config.RevisionDate,
            TargetSystemName = targetSystemName,
            TargetSystemMethod = targetSystemMethod,
            HasActiveJob = hasActiveJob,
            LastJobFailed = lastJobFailed,
        };
}
