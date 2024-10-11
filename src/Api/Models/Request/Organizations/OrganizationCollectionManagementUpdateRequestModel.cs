using Bit.Core;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Services;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
    public bool LimitCreateDeleteOwnerAdmin { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization, IFeatureService featureService)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.LimitCollectionCreationDeletionSplit))
        {
            existingOrganization.LimitCollectionCreation = LimitCollectionCreation;
            existingOrganization.LimitCollectionDeletion = LimitCollectionDeletion;
        }
        else
        {
            existingOrganization.LimitCollectionCreationDeletion = LimitCreateDeleteOwnerAdmin || LimitCollectionCreation || LimitCollectionDeletion;
        }

        existingOrganization.AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems;
        return existingOrganization;
    }
}
