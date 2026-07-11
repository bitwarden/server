using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.Rotation.Api.Models.Request;

/// <summary>The body of <c>PUT configs/{id}/account</c> (spec <c>UpdateRotationAccount</c>).</summary>
public class UpdateRotationAccountRequestModel
{
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    public bool TerminateSessions { get; set; }
}
