using Bit.Core.Models.Data;

namespace Bit.Api.Models.Public.Request;

public class AssociationWithPermissionsRequestModel : AssociationWithPermissionsBaseModel
{
    public SelectionReadOnly ToSelectionReadOnly()
    {
        return new SelectionReadOnly
        {
            Id = Id.Value,
            ReadOnly = ReadOnly.Value
        };
    }
}
