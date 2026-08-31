using System.ComponentModel.DataAnnotations;

namespace Bit.Services.Pam.AccessConnector.Rotation.Api.Models.Request;

/// <summary>The body of <c>PUT rotation/configs/{id}/account</c> (spec <c>UpdateRotationAccount</c>).</summary>
public class UpdateRotationAccountRequestModel
{
    /// <summary>The account this config rotates on the target system. Opaque to the server -- never parsed.</summary>
    [Required]
    [StringLength(500)]
    public string AccountIdentity { get; set; } = null!;

    /// <summary>
    /// When true, the access connector terminates the account's live sessions after each rotation. Only an automatic
    /// target that supports session termination can honour it.
    /// </summary>
    public bool TerminateSessions { get; set; }
}
