using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Models.Response;

public class ProfileProviderOrganizationResponseModel : BaseProfileOrganizationResponseModel
{
    public ProfileProviderOrganizationResponseModel(ProviderUserOrganizationDetails organization)
        : base("profileProviderOrganization", organization)
    {
        Status = OrganizationUserStatusType.Confirmed; // Provider users are always confirmed
        Type = OrganizationUserType.Owner; // Provider users behave like Owners
        ProviderId = organization.ProviderId;
        ProviderName = organization.ProviderName;
        ProviderType = organization.ProviderType;
        Permissions = new Permissions();
    }
}
