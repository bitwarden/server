using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Response;

public class ProfileOrganizationResponseModel : BaseProfileOrganizationResponseModel
{
    public ProfileOrganizationResponseModel(
        OrganizationUserOrganizationDetails organization,
        IEnumerable<Guid> organizationIdsClaimingUser)
        : base("profileOrganization", organization)
    {
        UseSecretsManager = organization.UseSecretsManager;
        UsePasswordManager = organization.UsePasswordManager;
        Status = organization.Status;
        Type = organization.Type;
        SsoBound = !string.IsNullOrWhiteSpace(organization.SsoExternalId);
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(organization.Permissions);
        ResetPasswordEnrolled = !string.IsNullOrWhiteSpace(organization.ResetPasswordKey);
        OrganizationUserId = organization.OrganizationUserId;
        FamilySponsorshipFriendlyName = organization.FamilySponsorshipFriendlyName;
        IsAdminInitiated = organization.IsAdminInitiated ?? false;
        FamilySponsorshipAvailable = (FamilySponsorshipFriendlyName == null || IsAdminInitiated) &&
            StaticStore.GetSponsoredPlan(PlanSponsorshipType.FamiliesForEnterprise)
            .UsersCanSponsor(organization);
        FamilySponsorshipLastSyncDate = organization.FamilySponsorshipLastSyncDate;
        FamilySponsorshipToDelete = organization.FamilySponsorshipToDelete;
        FamilySponsorshipValidUntil = organization.FamilySponsorshipValidUntil;
        AccessSecretsManager = organization.AccessSecretsManager;
        UserIsClaimedByOrganization = organizationIdsClaimingUser.Contains(organization.OrganizationId);
    }
}
