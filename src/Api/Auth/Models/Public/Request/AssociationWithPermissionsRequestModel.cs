using Bit.Core.Models.Data;

namespace Bit.Api.Auth.Models.Public.Request;

public class AssociationWithPermissionsRequestModel : AssociationWithPermissionsBaseModel
{
    public CollectionAccessSelection ToSelectionReadOnly()
    {
        return new CollectionAccessSelection
        {
            Id = Id.Value,
            ReadOnly = ReadOnly.Value,
            HidePasswords = HidePasswords.Value
        };
    }
}
