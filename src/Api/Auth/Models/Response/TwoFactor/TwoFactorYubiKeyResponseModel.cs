// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>POST /two-factor/get-yubikey</c>. Wraps the provider details and the
/// user-verification token minted by the GET endpoint.
/// </summary>
public class TwoFactorYubiKeyResponseModel : ResponseModel
{
    public TwoFactorYubiKeyResponseModel(User user, string userVerificationToken)
        : base("twoFactorYubiKey")
    {
        YubiKey = new TwoFactorYubiKeyDetails(user);
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorYubiKeyDetails YubiKey { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Replayed on subsequent
    /// management calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
