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

    public static PamRotationConfigDetails From(PamRotationConfig config, string targetSystemName,
        PamTargetSystemMethod targetSystemMethod, bool hasActiveJob) => new()
        {
            Id = config.Id,
            OrganizationId = config.OrganizationId,
            CipherId = config.CipherId,
            TargetSystemId = config.TargetSystemId,
            AccountIdentity = config.AccountIdentity,
            TerminateSessions = config.TerminateSessions,
            ScheduleCron = config.ScheduleCron,
            RotateOnAccessEnd = config.RotateOnAccessEnd,
            NextRotationAt = config.NextRotationAt,
            Enabled = config.Enabled,
            LastRotationAt = config.LastRotationAt,
            CreationDate = config.CreationDate,
            RevisionDate = config.RevisionDate,
            TargetSystemName = targetSystemName,
            TargetSystemMethod = targetSystemMethod,
            HasActiveJob = hasActiveJob,
        };
}
