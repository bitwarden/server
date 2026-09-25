using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update Authenticator provider details.
/// </summary>
public class TwoFactorAuthenticatorUpdateResponseModel : ResponseModel
{
    public TwoFactorAuthenticatorUpdateResponseModel(User user)
        : base("twoFactorAuthenticatorUpdate")
    {
        Authenticator = new TwoFactorAuthenticatorDetails(user);
    }

    public TwoFactorAuthenticatorDetails Authenticator { get; set; }
}
