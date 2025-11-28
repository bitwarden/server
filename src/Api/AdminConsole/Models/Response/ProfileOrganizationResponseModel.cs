using Bit.Core.Billing.Models;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response;

/// <summary>
/// Sync data for organization members and their organization.
/// Note: see <see cref="ProfileProviderOrganizationResponseModel"/> for organization sync data received by provider users.
/// </summary>
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
        IsAdminInitiated = organizationDetails.IsAdminInitiated ?? false;
        FamilySponsorshipFriendlyName = organizationDetails.FamilySponsorshipFriendlyName;
        FamilySponsorshipLastSyncDate = organizationDetails.FamilySponsorshipLastSyncDate;
        FamilySponsorshipToDelete = organizationDetails.FamilySponsorshipToDelete;
        FamilySponsorshipValidUntil = organizationDetails.FamilySponsorshipValidUntil;
        FamilySponsorshipAvailable = (organizationDetails.FamilySponsorshipFriendlyName == null || IsAdminInitiated) &&
            SponsoredPlans.Get(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organizationDetails);
        AccessSecretsManager = organizationDetails.AccessSecretsManager;
    }

    public Guid OrganizationUserId { get; set; }
    public bool UserIsClaimedByOrganization { get; set; }
    public string? FamilySponsorshipFriendlyName { get; set; }
    public bool FamilySponsorshipAvailable { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
    public bool IsAdminInitiated { get; set; }
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
