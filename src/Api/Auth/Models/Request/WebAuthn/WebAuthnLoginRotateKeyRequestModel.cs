using System.ComponentModel.DataAnnotations;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Api.Auth.Models.Request.WebAuthn;

public class WebAuthnLoginRotateKeyRequestModel
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

    public WebAuthnLoginRotateKeyData ToWebAuthnRotateKeyData()
    {
        return new WebAuthnLoginRotateKeyData
        {
            Id = Id,
            EncryptedUserKey = EncryptedUserKey,
            EncryptedPublicKey = EncryptedPublicKey,
        };
    }
}
