using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

/// <summary>
/// A named grouping of vault items within an organization, used to share items with members and groups.
/// Access is granted to individual members via <see cref="CollectionUser"/> and to groups via <see cref="CollectionGroup"/>.
/// </summary>
public class Collection : ITableObject<Guid>
{
    /// <summary>
    /// A unique identifier for the collection.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// The ID of the <see cref="Bit.Core.AdminConsole.Entities.Organization"/> that owns this collection.
    /// </summary>
    public Guid OrganizationId { get; set; }
    /// <summary>
    /// The name of the collection, stored encrypted.
    /// </summary>
    public string Name { get; set; } = null!;
    /// <summary>
    /// An ID used to associate this collection with a record in external services.
    /// </summary>
    [MaxLength(300)]
    public string? ExternalId { get; set; }
    /// <summary>
    /// The date the collection was created.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The date the collection was last updated.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// The type of collection, indicating how it was created and how it behaves.
    /// </summary>
    public CollectionType Type { get; set; } = CollectionType.SharedCollection;
    /// <summary>
    /// Used as the name for the collection if this is a <see cref="CollectionType.DefaultUserCollection"/>
    /// of a user who is no longer a member of the organization. In this case, it stores the user's
    /// email address so that it is identifiable by an admin (rather than being called "My Items" for an
    /// unknown user). Unencrypted.
    /// </summary>
    public string? DefaultUserCollectionEmail { get; set; }

    /// <summary>
    /// Initializes <see cref="Id"/> to a new COMB GUID.
    /// </summary>
    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
