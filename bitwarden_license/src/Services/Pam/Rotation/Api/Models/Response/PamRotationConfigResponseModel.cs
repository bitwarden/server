using Bit.HttpExtensions;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Models.Response;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation config's schedule-list view (spec derived predicates <c>has_active_job</c> /
/// <c>awaiting_manual_rotation</c> folded in) -- the view model for <c>GET rotation/configs</c> and the summary
/// embedded in <see cref="PamRotationConfigDetailResponseModel"/>.
/// </summary>
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

    /// <summary>
    /// The rotation config's unique identifier.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The organization this config belongs to.
    /// </summary>
    public Guid OrganizationId { get; }

    /// <summary>
    /// The organization cipher holding the credential this config rotates.
    /// </summary>
    public Guid CipherId { get; }

    /// <summary>
    /// The target system the rotated account lives on.
    /// </summary>
    public Guid TargetSystemId { get; }

    /// <summary>
    /// The target system's display name.
    /// </summary>
    public string TargetSystemName { get; }

    /// <summary>
    /// How the target's credentials are rotated -- see <see cref="PamTargetSystemMethod"/>. Decides whether a due
    /// rotation produces a job for a daemon or an obligation on an operator.
    /// </summary>
    public PamTargetSystemMethod TargetSystemMethod { get; }

    /// <summary>
    /// The account this config rotates on the target system. Opaque to the server -- never parsed; only the daemon
    /// interprets it.
    /// </summary>
    public string AccountIdentity { get; }

    /// <summary>
    /// When true, the daemon terminates the account's live sessions after each rotation.
    /// </summary>
    public bool TerminateSessions { get; }

    /// <summary>
    /// The scheduled-rotation cadence, as a Quartz 6-field cron expression evaluated in UTC. Null means no
    /// scheduled rotation.
    /// </summary>
    public string? ScheduleCron { get; }

    /// <summary>
    /// When true, the credential is rotated whenever an access lease on the cipher ends, whether it expired or was
    /// revoked.
    /// </summary>
    public bool RotateOnAccessEnd { get; }

    /// <summary>
    /// When the config is next due (UTC). On an automatic target the sweep offers a job once this is reached; on a
    /// manual target it instead marks the config <see cref="AwaitingManualRotation"/>. Null means nothing is due.
    /// </summary>
    public DateTime? NextRotationAt { get; }

    /// <summary>
    /// When false, the config is inactive and no rotation is offered for it.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// When the last rotation for this config completed successfully (UTC). Null until the first success.
    /// </summary>
    public DateTime? LastRotationAt { get; }

    /// <summary>
    /// Whether the config has a job in an active status -- spec <c>has_active_job</c>.
    /// </summary>
    public bool HasActiveJob { get; }

    /// <summary>
    /// A manual-target config whose schedule has come due -- spec <c>awaiting_manual_rotation</c>. There is no job
    /// to claim; an operator must record the out-of-band rotation via <c>POST configs/{id}/record-manual</c>.
    /// </summary>
    public bool AwaitingManualRotation { get; }

    /// <summary>
    /// When the config was created (UTC).
    /// </summary>
    public DateTime CreationDate { get; }

    /// <summary>
    /// When the config was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; }
}
