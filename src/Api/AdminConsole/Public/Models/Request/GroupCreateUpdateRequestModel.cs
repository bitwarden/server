using Bit.Core.AdminConsole.Entities;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class GroupCreateUpdateRequestModel : GroupBaseModel
{
    /// <summary>
    /// The associated collections that this group can access.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsRequestModel> Collections { get; set; }

    public Group ToGroup(Guid orgId)
    {
        return ToGroup(new Group { OrganizationId = orgId });
    }

    public Group ToGroup(Group existingGroup)
    {
        existingGroup.Name = Name;
        existingGroup.ExternalId = ExternalId;
        return existingGroup;
    }
}
