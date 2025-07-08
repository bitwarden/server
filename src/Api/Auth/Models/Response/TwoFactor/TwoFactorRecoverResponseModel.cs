// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

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
