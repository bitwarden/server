// FIXME: Update this file to be null safe and then delete the line below
#nullable disable


using Bit.Core.Models.Api;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Api.Response.Accounts;

public class WebAuthnLoginAssertionOptionsResponseModel : ResponseModel
{
    private const string ResponseObj = "webAuthnLoginAssertionOptions";

    public WebAuthnLoginAssertionOptionsResponseModel() : base(ResponseObj)
    {
    }

    public AssertionOptions Options { get; set; }
    public string Token { get; set; }
}

