using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Public.Models.Response;

public class AssociationWithPermissionsResponseModel : AssociationWithPermissionsBaseModel
{
    public AssociationWithPermissionsResponseModel(CollectionAccessSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        Id = selection.Id;
        ReadOnly = selection.ReadOnly;
        HidePasswords = selection.HidePasswords;
        Manage = selection.Manage;
    }
}
