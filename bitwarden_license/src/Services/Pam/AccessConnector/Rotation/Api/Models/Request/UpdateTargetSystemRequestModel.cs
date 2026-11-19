using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The updatable shape of a target system (spec <c>UpdateTargetSystem</c>).</summary>
public class UpdateTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>The target system's sealed blob.</summary>
    [Required]
    [StringLength(10000)]
    public string SealedData { get; set; } = null!;

    /// <summary>The content key of the sealed blob, wrapped by the organization key. Stored apart from the blob so organization key rotation only re-wraps the key.</summary>
    [Required]
    [StringLength(2000)]
    public string WrappedKey { get; set; } = null!;
}
