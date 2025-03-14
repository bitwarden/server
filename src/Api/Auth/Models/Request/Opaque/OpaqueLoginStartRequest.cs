using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class OpaqueLoginStartRequest
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string CredentialRequest { get; set; }

}
