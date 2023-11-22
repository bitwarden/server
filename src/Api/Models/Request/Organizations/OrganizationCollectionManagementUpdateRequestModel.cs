using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCreateDeleteOwnerAdmin { get; set; }

    public virtual Organization ToOrganization(Organization existingOrganization)
    {
        existingOrganization.LimitCollectionCreationDeletion = LimitCreateDeleteOwnerAdmin;
        return existingOrganization;
    }
}
