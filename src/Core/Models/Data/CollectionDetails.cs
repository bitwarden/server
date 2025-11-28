using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

/// <summary>
/// Collection information that includes permission details for a particular user
/// </summary>
public class CollectionDetails : Collection
{
    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
}
