using Bit.Core.Entities;

namespace Bit.Core.Models.Data;

/// <summary>
/// Collection information that includes permission details for a particular user.
/// The user ID is not recorded and should be clear from the context (usually the current authenticated user).
/// </summary>
public class CollectionDetails : Collection
{
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
