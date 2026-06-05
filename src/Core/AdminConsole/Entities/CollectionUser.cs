namespace Bit.Core.Entities;

/// <summary>
/// A join record that grants an <see cref="OrganizationUser"/> direct access to a <see cref="Collection"/>
/// with specific permissions. Access via group membership is represented separately by <see cref="CollectionGroup"/>.
/// </summary>
public class CollectionUser
{
    /// <summary>
    /// The ID of the <see cref="Collection"/> the organization user has access to.
    /// </summary>
    public Guid CollectionId { get; set; }
    /// <summary>
    /// The ID of the <see cref="OrganizationUser"/> who has access to the collection.
    /// </summary>
    public Guid OrganizationUserId { get; set; }
    /// <summary>
    /// If true, the user can view items in the collection but cannot create, edit, or delete them.
    /// </summary>
    public bool ReadOnly { get; set; }
    /// <summary>
    /// If true, the user cannot see password and TOTP fields for items in the collection.
    /// </summary>
    public bool HidePasswords { get; set; }
    /// <summary>
    /// If true, the user can manage the collection itself — including renaming it,
    /// deleting it, and assigning access for other users and groups.
    /// </summary>
    public bool Manage { get; set; }
}
