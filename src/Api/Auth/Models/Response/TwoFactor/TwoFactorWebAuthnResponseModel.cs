using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying WebAuthn provider details and the user-verification token minted
/// by the read step of two-factor enrollment.
/// </summary>
public class TwoFactorWebAuthnResponseModel : ResponseModel
{
    public TwoFactorWebAuthnResponseModel(User user, string userVerificationToken)
        : base("twoFactorWebAuthn")
    {
        WebAuthn = new TwoFactorWebAuthnDetails(user);
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorWebAuthnDetails WebAuthn { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Replayed on subsequent
    /// management calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
