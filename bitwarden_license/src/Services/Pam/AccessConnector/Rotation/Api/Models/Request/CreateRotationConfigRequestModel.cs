using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// Creates a rotation config for a cipher (spec <c>CreateRotationConfig</c>). <see cref="ScheduleCron"/> is a Quartz
/// 6-field cron expression; null means no scheduled rotation (on-demand and/or access-end only).
/// </summary>
public class CreateRotationConfigRequestModel
{
    /// <summary>
    /// The organization cipher holding the credential this config rotates. A cipher has at most one rotation
    /// config.
    /// </summary>
    [Required]
    public Guid CipherId { get; set; }

    /// <summary>The target system the rotated account lives on.</summary>
    [Required]
    public Guid TargetSystemId { get; set; }

    /// <summary>The account this config rotates on the target system. Opaque to the server -- never parsed.</summary>
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the access connector terminates the account's live sessions after each rotation. Only an automatic
    /// target that supports session termination can honour it.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>
    /// The scheduled-rotation cadence, as a Quartz 6-field cron expression evaluated in UTC. Null means no
    /// scheduled rotation -- the config rotates on demand and/or on access end only.
    /// </summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>
    /// When true, the credential is rotated whenever an access lease on the cipher ends, whether it expired or
    /// was revoked.
    /// </summary>
    public bool RotateOnAccessEnd { get; set; }
}
