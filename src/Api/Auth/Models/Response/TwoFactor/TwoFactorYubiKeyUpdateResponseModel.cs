// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /two-factor/yubikey</c>. Wraps the post-update provider details.
/// </summary>
public class TwoFactorYubiKeyUpdateResponseModel : ResponseModel
{
    public TwoFactorYubiKeyUpdateResponseModel(User user)
        : base("twoFactorYubiKeyUpdate")
    {
        YubiKey = new TwoFactorYubiKeyDetails(user);
    }

    public TwoFactorYubiKeyDetails YubiKey { get; set; }
}
