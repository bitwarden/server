namespace Bit.Core.Entities;

/// <summary>
/// A join record that grants a <see cref="Bit.Core.AdminConsole.Entities.Group"/> access to a <see cref="Collection"/>
/// with specific permissions. Direct user access is represented separately by <see cref="CollectionUser"/>.
/// </summary>
public class CollectionGroup
{
    /// <summary>
    /// The ID of the <see cref="Collection"/> the group has access to.
    /// </summary>
    public Guid CollectionId { get; set; }
    /// <summary>
    /// The ID of the <see cref="Bit.Core.AdminConsole.Entities.Group"/> that has access to the collection.
    /// </summary>
    public Guid GroupId { get; set; }
    /// <summary>
    /// If true, group members can view items in the collection but cannot create, edit, or delete them.
    /// </summary>
    public bool ReadOnly { get; set; }
    /// <summary>
    /// If true, group members cannot see password and TOTP fields for items in the collection.
    /// </summary>
    public bool HidePasswords { get; set; }
    /// <summary>
    /// If true, group members can manage the collection itself — including renaming it,
    /// deleting it, and assigning access for other users and groups.
    /// </summary>
    public bool Manage { get; set; }
}
