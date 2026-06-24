// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /two-factor/webauthn</c>. Wraps the post-update provider details.
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
