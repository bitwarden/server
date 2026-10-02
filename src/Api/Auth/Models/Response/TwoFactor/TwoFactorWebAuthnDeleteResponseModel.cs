using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying the updated WebAuthn provider state after a per-credential
/// removal. The credential delete shrinks the credential list rather than destroying the
/// provider, so the caller still gets the parent state back; provider-level deletes return
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
