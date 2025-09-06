namespace Bit.Core.AdminConsole.Models.Business;

public record OrganizationCollectionManagementSettings
{
    public bool LimitCollectionCreation { get; set; }
    public bool LimitCollectionDeletion { get; set; }
    public bool LimitItemDeletion { get; set; }
    public bool AllowAdminAccessToAllCollectionItems { get; set; }
}
