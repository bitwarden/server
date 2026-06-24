using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>DELETE /two-factor/webauthn</c> (per-credential). The operation modifies
/// the WebAuthn provider's credentials list rather than destroying the provider, so the
/// response carries the updated parent state. All other 2FA DELETE endpoints return
/// 204 No Content.
/// </summary>
public class TwoFactorWebAuthnDeleteResponseModel : ResponseModel
{
    public TwoFactorWebAuthnDeleteResponseModel(User user)
        : base("twoFactorWebAuthnDelete")
    {
        WebAuthn = new TwoFactorWebAuthnDetails(user);
    }

    public TwoFactorWebAuthnDetails WebAuthn { get; set; }
}
