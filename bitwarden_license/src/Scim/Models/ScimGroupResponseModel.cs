// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;

namespace Bit.Scim.Models;

public class ScimGroupResponseModel : BaseScimGroupModel
{
    public ScimGroupResponseModel()
        : base(true)
    {
        Meta = new ScimMetaModel("Group");
    }

    public ScimGroupResponseModel(Group group)
        : this()
    {
        Id = group.Id;
        DisplayName = group.Name;
        ExternalId = group.ExternalId;
        Meta.Created = group.CreationDate;
        Meta.LastModified = group.RevisionDate;
    }

    public Guid Id { get; set; }
    public ScimMetaModel Meta { get; private set; }
}
