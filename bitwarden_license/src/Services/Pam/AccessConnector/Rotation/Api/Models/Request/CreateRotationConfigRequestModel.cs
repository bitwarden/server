using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// Creates a rotation config for a cipher (spec <c>CreateRotationConfig</c>). Allowed against a
/// <c>PendingSeal</c> target — the config simply does not run until the target is sealed.
/// </summary>
public class CreateRotationConfigRequestModel
{
    /// <summary>The organization cipher to rotate. A cipher has at most one rotation config.</summary>
    [Required]
    public Guid CipherId { get; set; }

    [Required]
    public Guid TargetSystemId { get; set; }

    /// <summary>
    /// The config's sealed blob (spec <c>RotationConfigSealedV1</c>), opaque to the server: the account identity to
    /// rotate and whether to terminate sessions, sealed under the org key.
    /// </summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;

    /// <summary>A Quartz 6-field cron expression evaluated in UTC. Null means no scheduled rotation.</summary>
    [StringLength(100)]
    public string? ScheduleCron { get; set; }

    /// <summary>When true, the credential rotates whenever a lease on the cipher ends, however it ended.</summary>
    public bool RotateOnAccessEnd { get; set; }
}
