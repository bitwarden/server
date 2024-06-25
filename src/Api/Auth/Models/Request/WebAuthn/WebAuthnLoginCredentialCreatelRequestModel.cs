using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Request.WebAuthn;

public class WebAuthnLoginCredentialCreateRequestModel
{
    [Required]
    public AuthenticatorAttestationRawResponse DeviceResponse { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    public bool SupportsPrf { get; set; }

    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedUserKey { get; set; }

    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPublicKey { get; set; }

    [EncryptedString]
    [EncryptedStringLength(2000)]
    public string EncryptedPrivateKey { get; set; }
}
