using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Request;

public class SelectionReadOnlyRequestModel
{
    [Required]
    public string Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }

    public CollectionAccessSelection ToSelectionReadOnly()
    {
        return new CollectionAccessSelection
        {
            Id = new Guid(Id),
            ReadOnly = ReadOnly,
            HidePasswords = HidePasswords,
            Manage = Manage,
        };
    }
}
