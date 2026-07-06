using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT configs/{id}/settings</c> (spec <c>UpdateRotationSettings</c>). A null
/// <see cref="ScheduleCron"/> clears the config's schedule (recompute-on-edit).
/// </summary>
public class UpdateRotationSettingsRequestModel
{
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    public bool RotateOnAccessEnd { get; set; }
}
