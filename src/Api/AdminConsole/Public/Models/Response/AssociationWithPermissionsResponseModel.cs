using System.Text.Json.Serialization;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Public.Models.Response;

public class AssociationWithPermissionsResponseModel : AssociationWithPermissionsBaseModel
{
    [JsonConstructor]
    public AssociationWithPermissionsResponseModel() : base()
    {
    }

    public AssociationWithPermissionsResponseModel(CollectionAccessSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }
        Id = selection.Id;
        ReadOnly = selection.ReadOnly;
        HidePasswords = selection.HidePasswords;
        Manage = selection.Manage;
    }
}
