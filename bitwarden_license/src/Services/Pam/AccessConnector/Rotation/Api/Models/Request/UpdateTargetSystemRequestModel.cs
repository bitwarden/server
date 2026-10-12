using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// The updatable shape of a target system (spec <c>SealTargetSystem</c>). <see cref="SealedData"/> replaces any
/// previously stored blob wholesale and, on a <c>PendingSeal</c> target, moves it to <c>Active</c> — the only
/// transition out of that status. A <c>Disabled</c> target stays disabled.
/// </summary>
public class UpdateTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// The target system's sealed blob (spec <c>TargetSystemSealedV1</c>), opaque to the server: kind, password
    /// policy and session-termination capability, sealed under the org key.
    /// </summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;
}
