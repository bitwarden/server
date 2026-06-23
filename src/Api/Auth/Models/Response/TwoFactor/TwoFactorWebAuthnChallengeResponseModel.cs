using Bit.Core.Models.Api;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Envelope around <see cref="CredentialCreateOptions"/> that adds the user-verification token.
/// </summary>
public class TwoFactorWebAuthnChallengeResponseModel : ResponseModel
{
    public TwoFactorWebAuthnChallengeResponseModel()
        : base("twoFactorWebAuthnChallenge")
    {
    }

    /// <summary>FIDO2 registration ceremony options; passed straight to <c>navigator.credentials.create()</c>.</summary>
    public CredentialCreateOptions Options { get; set; } = null!;

    /// <summary>Token to replay on the subsequent PUT or DELETE so the user does not have to re-verify.</summary>
    public string UserVerificationToken { get; set; } = null!;
}
