using Fido2NetLib;
using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Webauthn;

public class WebAuthnCredentialRequestModel
{
    [Required]
    public AuthenticatorAttestationRawResponse DeviceResponse { get; set; }

    [Required]
    public string Name { get; set; }

    [Required]
    public string Token { get; set; }
}

