// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class AssociationWithPermissionsRequestModel : AssociationWithPermissionsBaseModel
{
    public CollectionAccessSelection ToCollectionAccessSelection()
    {
        var collectionAccessSelection = new CollectionAccessSelection
        {
            Id = Id.Value,
            ReadOnly = ReadOnly.Value,
            HidePasswords = HidePasswords.GetValueOrDefault(),
            Manage = Manage.GetValueOrDefault()
        };

        return collectionAccessSelection;
    }
}
