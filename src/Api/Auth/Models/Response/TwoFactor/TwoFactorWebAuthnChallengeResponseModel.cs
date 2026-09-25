using Bit.Core.Models.Api;
using Fido2NetLib;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response carrying FIDO2 registration options from the WebAuthn challenge step of
/// two-factor enrollment. No user-verification token is echoed back — the challenge step
/// replays the token minted by the WebAuthn GET, and that same token stays valid for the
/// subsequent update.
/// </summary>
public class TwoFactorWebAuthnChallengeResponseModel : ResponseModel
{
    public TwoFactorWebAuthnChallengeResponseModel()
        : base("twoFactorWebAuthnChallenge")
    {
    }

    /// <summary>FIDO2 registration ceremony options; passed straight to <c>navigator.credentials.create()</c>.</summary>
    public CredentialCreateOptions Options { get; set; } = null!;
}
