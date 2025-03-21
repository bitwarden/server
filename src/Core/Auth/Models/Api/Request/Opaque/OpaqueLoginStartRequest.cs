using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Opaque;

public class OpaqueLoginStartRequest
{
    [Required]
    public string Email { get; set; }
    [Required]
    public string CredentialRequest { get; set; }
}
