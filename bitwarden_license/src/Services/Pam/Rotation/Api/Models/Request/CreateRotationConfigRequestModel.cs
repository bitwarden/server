using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>
/// Creates a rotation config for a cipher (spec <c>CreateRotationConfig</c>). <see cref="ScheduleCron"/> is a Quartz
/// 6-field cron expression; null means no scheduled rotation (on-demand and/or access-end only).
/// </summary>
public class CreateRotationConfigRequestModel
{
    [Required]
    public Guid CipherId { get; set; }

    [Required]
    public Guid TargetSystemId { get; set; }

    /// <summary>The account this config rotates on the target system. Opaque to the server -- never parsed.</summary>
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    public bool TerminateSessions { get; set; }

    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    public bool RotateOnAccessEnd { get; set; }
}
