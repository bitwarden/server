using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response model carrying post-update organization-scoped Duo provider details.
/// </summary>
public class TwoFactorOrganizationDuoUpdateResponseModel : ResponseModel
{
    public TwoFactorOrganizationDuoUpdateResponseModel(Organization organization)
        : base("twoFactorOrganizationDuoUpdate")
    {
        Duo = new TwoFactorDuoDetails(organization);
    }

    public TwoFactorDuoDetails Duo { get; set; }
}
