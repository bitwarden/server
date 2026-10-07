using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The full updatable shape of a rotation config (spec <c>UpdateRotationConfig</c>). Every field is written as sent,
/// so send unchanged fields as they stand.
/// </summary>
public class UpdateRotationConfigRequestModel
{
    /// <summary>The account this config rotates on the target system, opaque to the server.</summary>
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the access connector terminates the account's live sessions after each rotation. Requires an
    /// automatic target that supports it.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null clears the schedule.</summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }
}
