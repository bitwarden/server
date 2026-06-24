using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>POST /two-factor/get-email</c>. Wraps the provider details and the
/// user-verification token minted by the GET endpoint.
/// </summary>
public class TwoFactorEmailResponseModel : ResponseModel
{
    public TwoFactorEmailResponseModel(User user, string userVerificationToken)
        : base("twoFactorEmail")
    {
        Email = new TwoFactorEmailDetails(user);
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorEmailDetails Email { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Replayed on subsequent
    /// management calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
