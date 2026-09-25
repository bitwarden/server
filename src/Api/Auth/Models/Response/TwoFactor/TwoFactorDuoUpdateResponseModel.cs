using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update user-scoped Duo provider details.
/// </summary>
public class TwoFactorDuoUpdateResponseModel : ResponseModel
{
    public TwoFactorDuoUpdateResponseModel(User user)
        : base("twoFactorDuoUpdate")
    {
        Duo = new TwoFactorDuoDetails(user);
    }

    public TwoFactorDuoDetails Duo { get; set; }
}
