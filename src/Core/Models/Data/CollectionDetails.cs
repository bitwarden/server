using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

public class CollectionDetails : Collection
{
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
}
