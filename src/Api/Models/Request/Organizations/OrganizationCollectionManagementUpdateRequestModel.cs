using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization)
    {
        existingOrganization.LimitCollectionCreation = LimitCollectionCreation;
        existingOrganization.LimitCollectionDeletion = LimitCollectionDeletion;
        existingOrganization.AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems;
        return existingOrganization;
    }
}
