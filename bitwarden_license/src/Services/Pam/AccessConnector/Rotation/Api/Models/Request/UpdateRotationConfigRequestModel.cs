using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The body of <c>PUT rotation/configs/{id}</c> (spec <c>UpdateRotationConfig</c>), which replaces the separate
/// settings and account operations. Carries the config's whole updatable shape: every field is written as sent, so
/// a caller editing one of them must send the others as they stand rather than omitting them.
/// </summary>
public class UpdateRotationConfigRequestModel
{
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
