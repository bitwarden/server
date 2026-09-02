using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update Email provider details.
/// </summary>
public class TwoFactorEmailUpdateResponseModel : ResponseModel
{
    public TwoFactorEmailUpdateResponseModel(User user)
        : base("twoFactorEmailUpdate")
    {
        Email = new TwoFactorEmailDetails(user);
    }

    public TwoFactorEmailDetails Email { get; set; }
}
