using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A rotation config with its derived rotation state.</summary>
public class PamRotationConfigResponseModel : ResponseModel
{
    public PamRotationConfigResponseModel(string obj = "pamRotationConfig")
        : base(obj)
    {
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CipherId { get; set; }

    public Guid TargetSystemId { get; set; }

    public string TargetSystemName { get; set; } = null!;

    /// <summary>Whether a due rotation yields a job for an access connector or an operator obligation.</summary>
    public PamTargetSystemMethod TargetSystemMethod { get; set; }

    /// <summary>The config's sealed blob.</summary>
    public string SealedData { get; set; } = null!;

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null means no scheduled rotation.</summary>
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }

    /// <summary>
    /// When the config is next due: an automatic target then gets a job, a manual one becomes
    /// <see cref="AwaitingManualRotation"/>. Null means nothing is due.
    /// </summary>
    public DateTime? NextRotationAt { get; set; }

    /// <summary>When false, the config is paused and no rotation is offered for it.</summary>
    public bool Enabled { get; set; }

    /// <summary>When the last successful rotation completed. Null until the first success.</summary>
    public DateTime? LastRotationAt { get; set; }

    /// <summary>
    /// Whether the config has a pending or claimed job, or a timed-out one the sweep has not yet recorded (spec
    /// <c>has_active_job</c>).
    /// </summary>
    public bool HasActiveJob { get; set; }

    /// <summary>
    /// An enabled manual-target config that has come due (spec <c>awaiting_manual_rotation</c>). An operator rotates
    /// it out of band and records that via <c>record-manual</c>.
    /// </summary>
    public bool AwaitingManualRotation { get; set; }

    public DateTime CreationDate { get; set; }

    public DateTime RevisionDate { get; set; }
}
