using System.ComponentModel.DataAnnotations;
using Bit.Pam.Enums;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>
/// Registers a target system's first phase (spec <c>RegisterTargetSystem</c>). The row is created
/// <see cref="PamTargetSystemStatus.PendingSeal"/>; the client follows up with a <c>PUT</c> carrying the sealed blob,
/// which is the only transition out of that status.
/// </summary>
public class RegisterTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>
    /// Rotated by an access connector (automatic) or by a person out of band (manual). Nullable so an omitted value
    /// is rejected rather than read as <see cref="PamTargetSystemMethod.Automatic"/>.
    /// </summary>
    [Required]
    [EnumDataType(typeof(PamTargetSystemMethod))]
    public PamTargetSystemMethod? Method { get; set; }
}
