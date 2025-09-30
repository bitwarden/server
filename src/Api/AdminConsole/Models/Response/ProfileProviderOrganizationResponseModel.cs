using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Models.Response;

/// <summary>
/// Sync data for provider users and their managed organizations.
/// Note: see <see cref="ProfileMemberOrganizationResponseModel"/> for organization sync data received by organization members.
/// </summary>
public class ProfileProviderOrganizationResponseModel : BaseProfileOrganizationResponseModel
{
    public ProfileProviderOrganizationResponseModel(ProviderUserOrganizationDetails organizationDetails)
        : base("profileProviderOrganization", organizationDetails)
    {
        Status = OrganizationUserStatusType.Confirmed; // Provider users are always confirmed
        Type = OrganizationUserType.Owner; // Provider users behave like Owners
        ProviderId = organizationDetails.ProviderId;
        ProviderName = organizationDetails.ProviderName;
        ProviderType = organizationDetails.ProviderType;
        Permissions = new Permissions();
    }
}
