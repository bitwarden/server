using System.ComponentModel.DataAnnotations;
using Bitwarden.OPAQUE;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class RegisterStartRequest
{
    [Required]
    public String ClientRegistrationStartResult { get; set; }
    [Required]
    public CipherConfiguration CipherConfiguration { get; set; }
}
