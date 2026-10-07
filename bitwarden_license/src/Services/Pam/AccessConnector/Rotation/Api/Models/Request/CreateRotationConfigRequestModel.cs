using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>Creates a rotation config for a cipher (spec <c>CreateRotationConfig</c>).</summary>
public class CreateRotationConfigRequestModel
{
    /// <summary>The organization cipher to rotate. A cipher has at most one rotation config.</summary>
    [Required]
    public Guid CipherId { get; set; }

    [Required]
    public Guid TargetSystemId { get; set; }

    /// <summary>The account this config rotates on the target system, opaque to the server.</summary>
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the access connector terminates the account's live sessions after each rotation. Requires an
    /// automatic target that supports it.
    /// </summary>
    public bool TerminateSessions { get; set; }

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null means no scheduled rotation.</summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }
}
