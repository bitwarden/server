using Bit.Core.Models.Api;

namespace Bit.Core.Auth.Models.Api.Response.Accounts;

public class WebAuthnCredentialAssertionResponseModel : ResponseModel
{
    private const string ResponseObj = "webauthnCredentialAssertion";

    public WebAuthnCredentialAssertionResponseModel() : base(ResponseObj)
    {
    }

    public string Token { get; set; }
}

