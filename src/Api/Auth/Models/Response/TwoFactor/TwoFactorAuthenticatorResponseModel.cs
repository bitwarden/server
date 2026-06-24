// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>POST /two-factor/get-authenticator</c>. Wraps the provider details
/// and the user-verification token minted by the GET endpoint.
/// </summary>
public class TwoFactorAuthenticatorResponseModel : ResponseModel
{
    // The Authenticator GET path mints a random key when the user has no existing provider; the
    // tokenable must be bound to the same key the response carries. This constructor lets the
    // caller hydrate the data once and pass it through, so a re-hydration cannot produce a
    // different key.
    public TwoFactorAuthenticatorResponseModel(TwoFactorAuthenticatorDetails authenticator, string userVerificationToken)
        : base("twoFactorAuthenticator")
    {
        Authenticator = authenticator;
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorAuthenticatorDetails Authenticator { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + Key</c>. Replayed on subsequent management
    /// calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
