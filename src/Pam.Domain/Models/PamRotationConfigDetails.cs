using Bit.Pam.Entities;
using Bit.Pam.Enums;

namespace Bit.Pam.Models;

/// <summary>
/// A <see cref="PamRotationConfig"/> together with its target system's display fields and whether it currently has
/// an active job — the list/detail view model for the rotation-configs admin surface.
/// </summary>
public class PamRotationConfigDetails : PamRotationConfig
{
    public string TargetSystemName { get; set; } = null!;
    public PamTargetSystemMethod TargetSystemMethod { get; set; }

    /// <summary>Whether the config has a Pending or Claimed job — spec <c>has_active_job</c>, see <c>PamRotationRules.IsActiveJobStatus</c>.</summary>
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
