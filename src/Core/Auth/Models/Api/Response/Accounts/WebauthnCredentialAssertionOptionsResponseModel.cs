using Bit.Core.Models.Api;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Api.Response.Accounts;

public class WebAuthnCredentialAssertionOptionsResponseModel : ResponseModel
{
    private const string ResponseObj = "webauthnCredentialAssertionOptions";

    public WebAuthnCredentialAssertionOptionsResponseModel() : base(ResponseObj)
    {
    }

    public AssertionOptions Options { get; set; }
    public string Token { get; set; }
}

