using Bitwarden.OPAQUE;

namespace Bit.Api.Auth.Models.Request.Opaque;

public class RegisterStartRequest
{
    public String ClientRegistrationStartResult { get; set; }
    public CipherConfiguration CipherConfiguration { get; set; }
}
