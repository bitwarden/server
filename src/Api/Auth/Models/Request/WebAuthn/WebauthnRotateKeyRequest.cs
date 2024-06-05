using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.Webauthn;

public class WebauthnRotateKeyRequestModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

    public WebauthnRotateKeyData ToWebauthnRotateKeyData()
    {
        return new WebauthnRotateKeyData
        {
            Id = Id,
            EncryptedUserKey = EncryptedUserKey,
            EncryptedPublicKey = EncryptedPublicKey
        };
    }

}
