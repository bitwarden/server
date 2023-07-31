using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCreateDeleteOwnerAdmin { get; set; }
    
    public virtual Organization ToOrganization(Organization existingOrganization)
    {
        existingOrganization.LimitCollectionCdOwnerAdmin = LimitCreateDeleteOwnerAdmin;
        return existingOrganization;
    }
}
