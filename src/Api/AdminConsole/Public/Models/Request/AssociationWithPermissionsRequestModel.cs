using Bit.Core.Exceptions;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Public.Models.Request;

public class AssociationWithPermissionsRequestModel : AssociationWithPermissionsBaseModel
{
    public CollectionAccessSelection ToCollectionAccessSelection(bool migratedToFlexibleCollections)
    {
        var collectionAccessSelection = new CollectionAccessSelection
        {
            Id = Id.Value,
            ReadOnly = ReadOnly.Value,
            HidePasswords = HidePasswords.GetValueOrDefault()
        };

        // Throws if the org has not migrated to use FC but has passed in a Manage value in the request
        if (!migratedToFlexibleCollections && Manage.HasValue)
        {
            throw new BadRequestException(
                "Your organization has not migrated to use Flexible Collections and cannot make use of the Manage property.");
        }

        collectionAccessSelection.Manage = Manage.GetValueOrDefault();
        return collectionAccessSelection;
    }
}
