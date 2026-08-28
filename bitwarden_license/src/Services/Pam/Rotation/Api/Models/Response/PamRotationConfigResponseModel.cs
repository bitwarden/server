using Bit.HttpExtensions;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.Rotation.Api.Models.Response;

/// <summary>
/// A rotation config's schedule-list view (spec derived predicates <c>has_active_job</c> /
/// <c>awaiting_manual_rotation</c> folded in) -- the view model for <c>GET rotation/configs</c> and the summary
/// embedded in <see cref="PamRotationConfigDetailResponseModel"/>.
/// </summary>
public class PamRotationConfigResponseModel : ResponseModel
{
    public PamRotationConfigResponseModel(string obj = "pamRotationConfig")
        : base(obj)
    {
    }

    /// <summary>
    /// The rotation config's unique identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The organization this config belongs to.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The organization cipher holding the credential this config rotates.
    /// </summary>
    public Guid CipherId { get; set; }

    /// <summary>
    /// The target system the rotated account lives on.
    /// </summary>
    public Guid TargetSystemId { get; set; }

    /// <summary>
    /// The target system's display name.
    /// </summary>
    public string TargetSystemName { get; set; } = null!;

    /// <summary>
    /// How the target's credentials are rotated -- see <see cref="PamTargetSystemMethod"/>. Decides whether a due
    /// rotation produces a job for a daemon or an obligation on an operator.
    /// </summary>
    public PamTargetSystemMethod TargetSystemMethod { get; set; }

    /// <summary>
    /// The account this config rotates on the target system. Opaque to the server -- never parsed; only the daemon
    /// interprets it.
    /// </summary>
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the daemon terminates the account's live sessions after each rotation.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>
    /// The scheduled-rotation cadence, as a Quartz 6-field cron expression evaluated in UTC. Null means no
    /// scheduled rotation.
    /// </summary>
    public string? ScheduleCron { get; set; }

    /// <summary>
    /// When true, the credential is rotated whenever an access lease on the cipher ends, whether it expired or was
    /// revoked.
    /// </summary>
    public bool RotateOnAccessEnd { get; set; }

    /// <summary>
    /// When the config is next due (UTC). On an automatic target the sweep offers a job once this is reached; on a
    /// manual target it instead marks the config <see cref="AwaitingManualRotation"/>. Null means nothing is due.
    /// </summary>
    public DateTime? NextRotationAt { get; set; }

    /// <summary>
    /// When false, the config is inactive and no rotation is offered for it.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// When the last rotation for this config completed successfully (UTC). Null until the first success.
    /// </summary>
    public DateTime? LastRotationAt { get; set; }

    /// <summary>
    /// Whether the config has a job in an active status -- spec <c>has_active_job</c>.
    /// </summary>
    public bool HasActiveJob { get; set; }

    /// <summary>
    /// A manual-target config whose schedule has come due -- spec <c>awaiting_manual_rotation</c>. There is no job
    /// to claim; an operator must record the out-of-band rotation via <c>POST configs/{id}/record-manual</c>.
    /// </summary>
    public bool AwaitingManualRotation { get; set; }

    /// <summary>
    /// When the config was created (UTC).
    /// </summary>
    public DateTime CreationDate { get; set; }

    /// <summary>
    /// When the config was last modified (UTC).
    /// </summary>
    public DateTime RevisionDate { get; set; }
}
