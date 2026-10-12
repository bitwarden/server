using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The full updatable shape of a rotation config (spec <c>UpdateRotationConfig</c>). Every field is written as sent,
/// so send unchanged fields as they stand.
/// </summary>
public class UpdateRotationConfigRequestModel
{
    /// <summary>
    /// The config's sealed blob (spec <c>RotationConfigSealedV1</c>), opaque to the server: the account identity to
    /// rotate and whether to terminate sessions, sealed under the org key. Replaces any previously stored blob
    /// wholesale.
    /// </summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null clears the schedule.</summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }
}
