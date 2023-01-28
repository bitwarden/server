using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.TwoFactor;

public class TwoFactorRecoverResponseModel : ResponseModel
{
    public TwoFactorRecoverResponseModel(User user)
        : base("twoFactorRecover")
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        Code = user.TwoFactorRecoveryCode;
    }

    public string Code { get; set; }
}
