namespace Bit.Core.Models.Data;

/// <summary>
/// Represents a user's or group's access to a collection, including their permission level.
/// </summary>
public class CollectionAccessSelection
{
    /// <summary>
    /// The ID of the user (<see cref="Bit.Core.Entities.OrganizationUser"/>) or group
    /// (<see cref="Bit.Core.AdminConsole.Entities.Group"/>) being granted access.
    /// This is ambiguous without further context.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// If true, the user or group can view items in the collection but cannot create, edit, or delete them.
    /// </summary>
    public bool ReadOnly { get; set; }
    /// <summary>
    /// If true, the user or group cannot see password and TOTP fields for items in the collection.
    /// </summary>
    public bool HidePasswords { get; set; }
    /// <summary>
    /// If true, the user or group can manage the collection itself — including renaming it,
    /// deleting it, and assigning access for other users and groups.
    /// </summary>
    public bool Manage { get; set; }
}
