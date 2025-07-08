// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using Bit.Api.AdminConsole.Public.Models.Response;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Public.Response;

/// <summary>
/// A collection.
/// </summary>
public class CollectionResponseModel : CollectionBaseModel, IResponseModel
{
    public CollectionResponseModel(Collection collection, IEnumerable<CollectionAccessSelection> groups)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        Id = collection.Id;
        ExternalId = collection.ExternalId;
        Groups = groups?.Select(c => new AssociationWithPermissionsResponseModel(c));
        Type = collection.Type;
    }

    /// <summary>
    /// String representing the object's type. Objects of the same type share the same properties.
    /// </summary>
    /// <example>collection</example>
    [Required]
    public string Object => "collection";
    /// <summary>
    /// The collection's unique identifier.
    /// </summary>
    /// <example>539a36c5-e0d2-4cf9-979e-51ecf5cf6593</example>
    [Required]
    public Guid Id { get; set; }
    /// <summary>
    /// The associated groups that this collection is assigned to.
    /// </summary>
    public IEnumerable<AssociationWithPermissionsResponseModel> Groups { get; set; }
    /// <summary>
    /// The type of this collection
    /// </summary>
    public CollectionType Type { get; set; }
}
