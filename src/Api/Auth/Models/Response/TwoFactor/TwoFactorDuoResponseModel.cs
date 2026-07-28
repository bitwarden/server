using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying user-scoped Duo provider details and the user-verification token
/// minted by the read step of two-factor enrollment.
/// </summary>
public class TwoFactorDuoResponseModel : ResponseModel
{
    public TwoFactorDuoResponseModel(User user, string userVerificationToken)
        : base("twoFactorDuo")
    {
        Duo = new TwoFactorDuoDetails(user);
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorDuoDetails Duo { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Replayed on subsequent
    /// management calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
