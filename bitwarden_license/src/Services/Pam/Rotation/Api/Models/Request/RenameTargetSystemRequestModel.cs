using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>PUT target-systems/{id}/name</c>. Display-only -- the id keys the daemon's connector resolver.</summary>
public class RenameTargetSystemRequestModel
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = null!;
}
