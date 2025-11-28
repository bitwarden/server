// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public abstract class BaseScimGroupModel : BaseScimModel
{
    public BaseScimGroupModel(bool initSchema = false)
    {
        if (initSchema)
        {
            Schemas = new List<string> { ScimConstants.Scim2SchemaGroup };
        }
    }

    public string DisplayName { get; set; }
    public string ExternalId { get; set; }
}
