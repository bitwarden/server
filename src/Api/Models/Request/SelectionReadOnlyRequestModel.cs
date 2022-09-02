using System.ComponentModel.DataAnnotations;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Request;

public class SelectionReadOnlyRequestModel
{
    [Required]
    public string Id { get; set; }
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }

    public SelectionReadOnly ToSelectionReadOnly()
    {
        return new SelectionReadOnly
        {
            Id = new Guid(Id),
            ReadOnly = ReadOnly,
            HidePasswords = HidePasswords,
        };
    }
}
