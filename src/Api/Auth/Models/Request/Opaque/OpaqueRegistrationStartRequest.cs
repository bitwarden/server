using System.ComponentModel.DataAnnotations;
using Bitwarden.OPAQUE;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class OpaqueRegistrationStartRequest
{
    [Required]
    public String RegistrationRequest { get; set; }
    [Required]
    public CipherConfiguration CipherConfiguration { get; set; }
}
