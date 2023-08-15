using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Request;

public class SelectionReadOnlyRequestModel
{
    [Required]
    public Guid Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }

    public CollectionAccessSelection ToSelectionReadOnly()
    {
        return new CollectionAccessSelection
        {
            Id = Id,
            ReadOnly = ReadOnly,
            HidePasswords = HidePasswords,
            Manage = Manage,
        };
    }
}
