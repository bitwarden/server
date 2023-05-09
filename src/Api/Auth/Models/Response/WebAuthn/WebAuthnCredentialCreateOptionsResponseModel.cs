using Fido2NetLib;

namespace Bit.Api.Auth.Models.Response.WebAuthn;

public class WebAuthnCredentialCreateOptionsResponseModel
{
    public CredentialCreateOptions Options { get; set; }
    public string Token { get; set; }
}
