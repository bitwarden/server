// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>POST /two-factor/get-webauthn</c>. Wraps the provider details and the
/// user-verification token minted by the GET endpoint.
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
