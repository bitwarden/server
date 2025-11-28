// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Response.WebAuthn;

public class WebAuthnCredentialCreateOptionsResponseModel : ResponseModel
{
    private const string ResponseObj = "webauthnCredentialCreateOptions";

    public WebAuthnCredentialCreateOptionsResponseModel() : base(ResponseObj)
    {
    }

    public CredentialCreateOptions Options { get; set; }
    public string Token { get; set; }
}
