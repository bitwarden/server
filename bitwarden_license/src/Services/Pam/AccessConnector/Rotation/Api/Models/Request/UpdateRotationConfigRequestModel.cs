using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The full updatable shape of a rotation config (spec <c>UpdateRotationConfig</c>). Every field is written as sent,
/// so send unchanged fields as they stand.
/// </summary>
public class UpdateRotationConfigRequestModel
{
    /// <summary>The config's sealed blob.</summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;

    /// <summary>The content key of the sealed blob, wrapped by the organization key.</summary>
    [Required]
    [StringLength(2000)]
    public string WrappedKey { get; set; } = null!;

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null clears the schedule.</summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }
}
