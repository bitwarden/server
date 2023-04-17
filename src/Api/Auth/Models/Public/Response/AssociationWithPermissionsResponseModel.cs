using Bit.Core.Models.Data;

namespace Bit.Api.Auth.Models.Public.Response;

public class AssociationWithPermissionsResponseModel : AssociationWithPermissionsBaseModel
{
    public AssociationWithPermissionsResponseModel(CollectionAccessSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }
        Id = selection.Id;
        ReadOnly = selection.ReadOnly;
    }
}
