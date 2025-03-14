using System.ComponentModel.DataAnnotations;
using Bitwarden.OPAQUE;

namespace Bit.Core.Auth.Models.Api.Request.Opaque;

public class OpaqueRegistrationStartRequest
{
    [Required]
    public string RegistrationRequest { get; set; }
    [Required]
    public CipherConfiguration CipherConfiguration { get; set; }
}
