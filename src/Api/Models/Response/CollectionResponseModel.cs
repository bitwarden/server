using Bit.Core.Entities;
using Bit.Core.Models.Api;
using Bit.Core.Models.Data;

namespace Bit.Api.Models.Response;

public class CollectionResponseModel : ResponseModel
{
    public CollectionResponseModel(Collection collection, string obj = "collection")
        : base(obj)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }

        Id = collection.Id;
        OrganizationId = collection.OrganizationId;
        Name = collection.Name;
        ExternalId = collection.ExternalId;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; }
    public string ExternalId { get; set; }
}

/// <summary>
/// Response model for a collection that is always assigned to the requesting user, including permissions.
/// </summary>
public class CollectionDetailsResponseModel : CollectionResponseModel
{
    /// <summary>
    /// Create a response model for when the user is assumed to be assigned to the collection with permissions.
    /// e.g. The collection details comes from a repository method that only returns collections the user is assigned to.
    /// </summary>
    public CollectionDetailsResponseModel(CollectionDetails collectionDetails)
        : base(collectionDetails, "collectionDetails")
    {
        ReadOnly = collectionDetails.ReadOnly;
        HidePasswords = collectionDetails.HidePasswords;
        Manage = collectionDetails.Manage;
    }

    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
}

public class CollectionAccessDetailsResponseModel : CollectionResponseModel
{
    /// <summary>
    /// Create a response model for when the requesting user is assumed not assigned to the collection.
    /// No user permissions are included.
    ///
    /// Ideally, the CollectionAdminDetails constructor should be used instead wherever possible. This is only
    /// used in the case of MSPs where the Provider user will likely never be assigned to the collection.
    /// </summary>
    /// <param name="collection"></param>
    public CollectionAccessDetailsResponseModel(Collection collection)
        : base(collection, "collectionAccessDetails")
    { }

    /// <summary>
    /// Create a response model for when the requesting user is assumed not assigned to the collection. Includes
    /// the other groups and user relationships for the collection.
    /// No user permissions are included.
    /// </summary>
    /// <param name="collection"></param>
    /// <param name="groups"></param>
    /// <param name="users"></param>
    [Obsolete("Use the CollectionAdminDetails constructor instead.")]
    public CollectionAccessDetailsResponseModel(Collection collection, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users)
        : base(collection, "collectionAccessDetails")
    {
        Groups = groups.Select(g => new SelectionReadOnlyResponseModel(g));
        Users = users.Select(g => new SelectionReadOnlyResponseModel(g));
    }

    /// <summary>
    /// Create a response model for when the requesting user's assignment is available via CollectionAdminDetails.
    /// </summary>
    /// <param name="collection"></param>
    public CollectionAccessDetailsResponseModel(CollectionAdminDetails collection)
        : base(collection, "collectionAccessDetails")
    {
        Assigned = collection.Assigned;
        ReadOnly = collection.ReadOnly;
        HidePasswords = collection.HidePasswords;
        Manage = collection.Manage;
        Unmanaged = collection.Unmanaged;
        Groups = collection.Groups?.Select(g => new SelectionReadOnlyResponseModel(g)) ?? Enumerable.Empty<SelectionReadOnlyResponseModel>();
        Users = collection.Users?.Select(g => new SelectionReadOnlyResponseModel(g)) ?? Enumerable.Empty<SelectionReadOnlyResponseModel>();
    }

    public IEnumerable<SelectionReadOnlyResponseModel> Groups { get; set; }
    public IEnumerable<SelectionReadOnlyResponseModel> Users { get; set; }

    /// <summary>
    /// True if the acting user is explicitly assigned to the collection
    /// </summary>
    public bool Assigned { get; set; }

    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }
    public bool Unmanaged { get; set; }
}
