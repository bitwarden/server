using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT configs/{id}/settings</c> (spec <c>UpdateRotationSettings</c>). A null
/// <see cref="ScheduleCron"/> clears the config's schedule (recompute-on-edit).
/// </summary>
public class UpdateRotationSettingsRequestModel
{
    /// <summary>
    /// The scheduled-rotation cadence, as a Quartz 6-field cron expression evaluated in UTC. Null clears the
    /// schedule, leaving the config to rotate on demand and/or on access end only.
    /// </summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>
    /// When true, the credential is rotated whenever an access lease on the cipher ends, whether it expired or
    /// was revoked.
    /// </summary>
    public bool RotateOnAccessEnd { get; set; }
}
