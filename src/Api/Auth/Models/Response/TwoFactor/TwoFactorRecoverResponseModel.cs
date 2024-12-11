using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

public class TwoFactorRecoverResponseModel : ResponseModel
{
    public TwoFactorRecoverResponseModel(User user)
        : base("twoFactorRecover")
    {
        ArgumentNullException.ThrowIfNull(user);

        Code = user.TwoFactorRecoveryCode;
    }

    public string Code { get; set; }
}
