using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
    public bool LimitCreateDeleteOwnerAdmin { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization)
    {
        existingOrganization.LimitCollectionCreation = LimitCollectionCreation;
        existingOrganization.LimitCollectionDeletion = LimitCollectionDeletion;
        // Deprecated: https://bitwarden.atlassian.net/browse/PM-10863
        existingOrganization.LimitCollectionCreationDeletion = LimitCreateDeleteOwnerAdmin;
        existingOrganization.AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems;
        return existingOrganization;
    }
}
