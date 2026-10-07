// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Models.Data;

/// <summary>
/// Access configuration for a collection, including all assigned groups and users and their permission levels.
/// </summary>
public class CollectionAccessDetails
{
    /// <summary>
    /// The groups that have been assigned to this collection, including each group's permission level.
    /// </summary>
    public IEnumerable<CollectionAccessSelection> Groups { get; set; }
    /// <summary>
    /// The organization users that have been directly assigned to this collection, including each user's permission level.
    /// </summary>
    public IEnumerable<CollectionAccessSelection> Users { get; set; }
}

