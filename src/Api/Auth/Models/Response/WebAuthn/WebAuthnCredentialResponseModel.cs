using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.WebAuthn;

public class WebAuthnCredentialResponseModel : ResponseModel
{
    private const string ResponseObj = "webauthnCredential";

    public WebAuthnCredentialResponseModel(WebAuthnCredential credential) : base(ResponseObj)
    {
        Id = credential.Id.ToString();
        Name = credential.Name;
        PrfStatus = credential.GetPrfStatus();
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public WebAuthnPrfStatus PrfStatus { get; set; }
}
