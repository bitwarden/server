using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update YubiKey provider details.
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
