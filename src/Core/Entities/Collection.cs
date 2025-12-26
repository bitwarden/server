using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class Collection : ITableObject<Guid>
{
    /// <summary>
    /// The unique identifier for the collection.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The identifier of the organization that owns this collection.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// The name of the collection, encrypted with the organization symmetric key.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// An external identifier for integration with external systems.
    /// </summary>
    [MaxLength(300)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// The date and time when the collection was created.
    /// </summary>
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The date and time when the collection was last revised.
    /// </summary>
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The type of collection - see <see cref="CollectionType"/>.
    /// </summary>
    public CollectionType Type { get; set; } = CollectionType.SharedCollection;

    /// <summary>
    /// The email address for the OrganizationUser associated with a DefaultUserCollection collection type.
    /// </summary>
    /// <remarks>
    /// This is only populated at the time the OrganizationUser leaves or is removed from the organization.
    /// It is then used as the collection name so that administrators can identify the collection.
    /// It is null for all other collection types and at all other times.
    /// </remarks>
    public string? DefaultUserCollectionEmail { get; set; }

    /// <summary>
    /// The ID for the OrganizationUser associated with this default collection.
    /// INTERNAL DATABASE USE ONLY - DO NOT USE - SEE REMARKS.
    /// </summary>
    /// <remarks>
    /// This is used to enforce uniqueness so that an OrganizationUser can only have 1 default collection
    /// in a given organization.
    /// It should NOT be used for any other purpose, used in the application code, or exposed to the front-end.
    /// In particular, refer to the <see cref="CollectionUser"/> to evaluate user assignment and permissions.
    /// Only populated for collections of type DefaultUserCollection.
    /// Set to null when the OrganizationUser entry is deleted.
    /// </remarks>
    public Guid? DefaultCollectionOwnerId { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
