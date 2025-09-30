using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response;

public class ProfileOrganizationResponseModel : BaseProfileOrganizationResponseModel
{
    public ProfileOrganizationResponseModel(
        OrganizationUserOrganizationDetails organizationDetails,
        IEnumerable<Guid> organizationIdsClaimingUser)
        : base("profileOrganization", organizationDetails)
    {
        Status = organizationDetails.Status;
        Type = organizationDetails.Type;
        OrganizationUserId = organizationDetails.OrganizationUserId;
        UserIsClaimedByOrganization = organizationIdsClaimingUser.Contains(organizationDetails.OrganizationId);
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organizationDetails.Permissions);
        FamilySponsorshipAvailable = (organizationDetails.FamilySponsorshipFriendlyName == null || IsAdminInitiated) &&
            StaticStore.GetSponsoredPlan(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organizationDetails);
        AccessSecretsManager = organizationDetails.AccessSecretsManager;
    }

    public Guid OrganizationUserId { get; set; }
    public bool UserIsClaimedByOrganization { get; set; }
    /// <summary>
    /// Obsolete property for backward compatibility
    /// </summary>
    [Obsolete("Please use UserIsClaimedByOrganization instead. This property will be removed in a future version.")]
    public bool UserIsManagedByOrganization
    {
        get => UserIsClaimedByOrganization;
        set => UserIsClaimedByOrganization = value;
    }
}
