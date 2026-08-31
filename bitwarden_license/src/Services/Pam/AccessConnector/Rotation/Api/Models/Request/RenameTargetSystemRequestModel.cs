using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The body of <c>PUT rotation/target-systems/{id}/name</c>.</summary>
public class RenameTargetSystemRequestModel
{
    /// <summary>The target system's new display name.</summary>
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;
}
