// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /two-factor/duo</c>. Wraps the post-update user-scoped Duo provider details.
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
