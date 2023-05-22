using System.ComponentModel.DataAnnotations;
using Fido2NetLib;

namespace Bit.Core.Auth.Models.Api.Request.Accounts;

public class WebauthnCredentialAssertionRequestModel
{
    [Required]
    public AuthenticatorAssertionRawResponse DeviceResponse { get; set; }

    [Required]
    public string Token { get; set; }
}

