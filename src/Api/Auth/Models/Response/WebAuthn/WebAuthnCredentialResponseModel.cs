using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Models.Api;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Response.WebAuthn;

public class WebAuthnCredentialResponseModel : ResponseModel
{
    private const string ResponseObj = "webauthnCredential";

    public WebAuthnCredentialResponseModel(WebAuthnCredential credential)
        : base(ResponseObj)
    {
        Id = credential.Id.ToString();
        Name = credential.Name;
        PrfStatus = credential.GetPrfStatus();
        EncryptedUserKey = credential.EncryptedUserKey;
        EncryptedPublicKey = credential.EncryptedPublicKey;
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public WebAuthnPrfStatus PrfStatus { get; set; }

    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }
}
