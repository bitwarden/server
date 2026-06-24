// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /two-factor/authenticator</c>. Wraps the post-update provider details.
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
