using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The updatable shape of a target system (spec <c>SealTargetSystem</c>).</summary>
public class UpdateTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>The target system's sealed blob.</summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;
}
