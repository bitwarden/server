using Bit.Core.Models.Data;

namespace Bit.Api.Models.Response;

public class SelectionReadOnlyResponseModel
{
    public SelectionReadOnlyResponseModel(CollectionAccessSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        Id = selection.Id;
        ReadOnly = selection.ReadOnly;
        HidePasswords = selection.HidePasswords;
        Manage = selection.Manage;
    }

    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
}
