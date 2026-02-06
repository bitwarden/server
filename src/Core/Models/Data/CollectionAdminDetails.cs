#nullable enable
namespace Bit.Core.Models.Data;

/// <summary>
/// Collection information that includes permission details for a particular user along with optional
/// access relationships for Groups/Users. Used for collection management.
/// </summary>
public class CollectionAdminDetails : CollectionDetails
{
    public IEnumerable<CollectionAccessSelection> Groups { get; set; } = [];
    public IEnumerable<CollectionAccessSelection> Users { get; set; } = [];

    /// <summary>
    /// Flag for whether the user has been explicitly assigned to the collection either directly or through a group.
    /// </summary>
    public bool Assigned { get; set; }

    /// <summary>
    /// Flag for whether a collection is managed by an active user or group.
    /// </summary>
    public bool Unmanaged { get; set; }
}
