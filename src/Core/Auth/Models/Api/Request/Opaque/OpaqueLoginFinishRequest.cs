using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class OpaqueLoginFinishRequest
{
    [Required]
    public string CredentialFinalization { get; set; }
    [Required]
    public Guid SessionId { get; set; }

}
