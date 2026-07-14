using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying organization-scoped Duo provider details and the user-verification
/// token minted by the read step of two-factor enrollment.
/// </summary>
public class TwoFactorOrganizationDuoResponseModel : ResponseModel
{
    public TwoFactorOrganizationDuoResponseModel(Organization organization, string userVerificationToken)
        : base("twoFactorOrganizationDuo")
    {
        Duo = new TwoFactorDuoDetails(organization);
        UserVerificationToken = userVerificationToken;
    }

    public TwoFactorDuoDetails Duo { get; set; }

    /// <summary>
    /// User-verification token bound to <c>UserId + ProviderType</c>. Replayed on subsequent
    /// management calls so the user does not have to re-verify.
    /// </summary>
    public string UserVerificationToken { get; set; }
}
