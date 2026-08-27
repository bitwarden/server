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
        // Custom permissions only apply to the Custom role, and the stored blob is not guaranteed to be cleared when
        // a member moves off Custom. This mirrors how the role's claims are built.
        Permissions = Type == OrganizationUserType.Custom
            ? CoreHelpers.LoadClassFromJsonData<Permissions>(organizationDetails.Permissions)
            : new Permissions();
        IsAdminInitiated = organizationDetails.IsAdminInitiated ?? false;
        FamilySponsorshipFriendlyName = organizationDetails.FamilySponsorshipFriendlyName;
        FamilySponsorshipLastSyncDate = organizationDetails.FamilySponsorshipLastSyncDate;
        FamilySponsorshipToDelete = organizationDetails.FamilySponsorshipToDelete;
        FamilySponsorshipValidUntil = organizationDetails.FamilySponsorshipValidUntil;
        FamilySponsorshipAvailable = (organizationDetails.FamilySponsorshipFriendlyName == null || IsAdminInitiated) &&
            SponsoredPlans.Get(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organizationDetails);
        AccessSecretsManager = organizationDetails.AccessSecretsManager;
        AccessPam = organizationDetails.AccessPam;
    }

    public Guid OrganizationUserId { get; set; }
    public bool UserIsClaimedByOrganization { get; set; }
    public string? FamilySponsorshipFriendlyName { get; set; }
    public bool FamilySponsorshipAvailable { get; set; }
    public DateTime? FamilySponsorshipLastSyncDate { get; set; }
    public DateTime? FamilySponsorshipValidUntil { get; set; }
    public bool? FamilySponsorshipToDelete { get; set; }
    public bool IsAdminInitiated { get; set; }
}
