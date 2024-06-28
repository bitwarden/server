using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Request.WebAuthn;

public class WebAuthnLoginCredentialUpdateRequestModel
{
    [Required]
    public AuthenticatorAssertionRawResponse DeviceResponse { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

    [Required]
    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPrivateKey { get; set; }
}
