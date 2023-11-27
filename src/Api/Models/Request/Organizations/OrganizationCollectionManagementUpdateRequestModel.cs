using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCreateDeleteOwnerAdmin { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization)
    {
        existingOrganization.LimitCollectionCreationDeletion = LimitCreateDeleteOwnerAdmin;
        existingOrganization.AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems;
        return existingOrganization;
    }
}
