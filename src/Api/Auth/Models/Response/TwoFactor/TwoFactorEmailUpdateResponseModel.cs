// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /two-factor/email</c>. Wraps the post-update provider details.
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
