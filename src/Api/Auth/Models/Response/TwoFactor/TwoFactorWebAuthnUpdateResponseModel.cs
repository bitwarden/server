using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update WebAuthn provider details.
/// </summary>
public class TwoFactorWebAuthnUpdateResponseModel : ResponseModel
{
    public TwoFactorWebAuthnUpdateResponseModel(User user)
        : base("twoFactorWebAuthnUpdate")
    {
        WebAuthn = new TwoFactorWebAuthnDetails(user);
    }

    public TwoFactorWebAuthnDetails WebAuthn { get; set; }
}
