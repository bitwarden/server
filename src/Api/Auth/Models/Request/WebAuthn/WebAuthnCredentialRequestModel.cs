using System.ComponentModel.DataAnnotations;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Request.Webauthn;

public class WebAuthnCredentialRequestModel
{
    [Required]
    public AuthenticatorAttestationRawResponse DeviceResponse { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Token { get; set; }

    [Required]
    public bool SupportsPrf { get; set; }

    public string UserKey { get; set; }
    public string PrfPublicKey { get; set; }
    public string PrfPrivateKey { get; set; }
}

