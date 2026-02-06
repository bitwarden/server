using Bit.Core.AdminConsole.Models.Business;

namespace Bit.Api.Models.Request.Organizations;

public class OrganizationCollectionManagementUpdateRequestModel
{
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool LimitItemDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }

    public OrganizationCollectionManagementSettings ToSettings() => new()
    {
        LimitCollectionCreation = LimitCollectionCreation,
        LimitCollectionDeletion = LimitCollectionDeletion,
        LimitItemDeletion = LimitItemDeletion,
        AllowAdminAccessToAllCollectionItems = AllowAdminAccessToAllCollectionItems
    };
}
