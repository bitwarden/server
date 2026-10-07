#nullable enable

namespace Bit.Core.Tools.Models.Data;

/// <summary>
/// Unencrypted metadata of an Item Send.
/// </summary>
public class SendItemMetadata
{
    /// <summary>
    /// Id of the vault item (cipher) being sent.
    /// </summary>
    public Guid ItemId { get; set; }
    /// <summary>
    /// The name of the folder the vault item being sent belongs to.
    /// </summary>
    public string? FolderName { get; set; }
    /// <summary>
    /// The names of the collections the vault item being sent belongs to.
    /// </summary>
    public string[]? CollectionNames { get; set; }
    /// <summary>
    /// The name of the organization the vault item being sent belongs to.
    /// </summary>
    public string? OrganizationName { get; set; }
    /// <summary>
    /// The date the vault item being shared was created
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the vault item being shared was last updated
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
}
