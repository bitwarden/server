using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Response;

/// <summary>A rotation config with its derived rotation state.</summary>
public class PamRotationConfigResponseModel : ResponseModel
{
    public PamRotationConfigResponseModel(
        PamRotationConfigDetails config, bool awaitingManualRotation, string obj = "pamRotationConfig")
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(config);

        Id = config.Id;
        OrganizationId = config.OrganizationId;
        CipherId = config.CipherId;
        TargetSystemId = config.TargetSystemId;
        TargetSystemName = config.TargetSystemName;
        TargetSystemMethod = config.TargetSystemMethod;
        AccountIdentity = config.AccountIdentity;
        TerminateSessions = config.TerminateSessions;
        ScheduleCron = config.ScheduleCron;
        RotateOnAccessEnd = config.RotateOnAccessEnd;
        NextRotationAt = config.NextRotationAt.AsUtc();
        Enabled = config.Enabled;
        LastRotationAt = config.LastRotationAt.AsUtc();
        HasActiveJob = config.HasActiveJob;
        AwaitingManualRotation = awaitingManualRotation;
        CreationDate = config.CreationDate.AsUtc();
        RevisionDate = config.RevisionDate.AsUtc();
    }

    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid CipherId { get; set; }

    public Guid TargetSystemId { get; set; }

    public string TargetSystemName { get; set; } = null!;

    /// <summary>Whether a due rotation yields a job for an access connector or an operator obligation.</summary>
    public PamTargetSystemMethod TargetSystemMethod { get; set; }

    /// <summary>The account this config rotates on the target system, opaque to the server.</summary>
    public string AccountIdentity { get; set; } = null!;

    /// <summary>When true, the access connector terminates the account's live sessions after each rotation.</summary>
    public bool TerminateSessions { get; set; }

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
