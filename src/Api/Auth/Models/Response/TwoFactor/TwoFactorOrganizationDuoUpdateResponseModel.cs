// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response.TwoFactor;

/// <summary>
/// Response for <c>PUT /organizations/{id}/two-factor/duo</c>. Wraps the post-update
/// organization-scoped Duo provider details.
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
