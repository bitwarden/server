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
/// Response model for a collection that includes a user's permission when that user is assigned to the collection.
/// </summary>
public class CollectionDetailsResponseModel : CollectionResponseModel
{
    /// <summary>
    /// Create a response model for when the user is not assigned to the collection
    /// (they have no explicit permissions/assignment).
    /// </summary>
    private CollectionDetailsResponseModel(Collection collection)
        : base(collection, "collectionDetails")
    {
        ReadOnly = false;
        HidePasswords = false;
        Manage = false;
        Assigned = false;
    }

    /// <summary>
    /// Create a response model for when the user is assumed to be assigned to the collection with permissions.
    /// e.g. The collection details comes from a repository method that only returns collections the user is assigned to.
    /// </summary>
    private CollectionDetailsResponseModel(CollectionDetails collectionDetails)
        : base(collectionDetails, "collectionDetails")
    {
        ReadOnly = collectionDetails.ReadOnly;
        HidePasswords = collectionDetails.HidePasswords;
        Manage = collectionDetails.Manage;
        Assigned = true;
    }

    /// <summary>
    /// Create a response model for when a user may or may not be assigned to the collection with permissions.
    /// </summary>
    public CollectionDetailsResponseModel(CollectionAdminDetails collection)
        : base(collection, "collectionDetails")
    {
        ReadOnly = collection.ReadOnly;
        HidePasswords = collection.HidePasswords;
        Manage = collection.Manage;
        Assigned = collection.Assigned;
    }

    public bool ReadOnly { get; set; }
    public bool HidePasswords { get; set; }
    public bool Manage { get; set; }

    /// <summary>
    /// Flag for whether the user has been explicitly assigned to the collection either directly or through a group.
    /// </summary>
    public bool Assigned { get; set; }


    /// <summary>
    /// Create a response model for when the user is assigned to the collection with permissions.
    /// </summary>
    public static CollectionDetailsResponseModel FromAssigned(CollectionDetails collectionDetails)
    {
        return new CollectionDetailsResponseModel(collectionDetails);
    }

    /// <summary>
    /// Create a response model for when the user is NOT assigned to the collection.
    /// </summary>
    public static CollectionDetailsResponseModel FromUnassigned(Collection collection)
    {
        return new CollectionDetailsResponseModel(collection);
    }
}

public class CollectionAccessDetailsResponseModel : CollectionResponseModel
{
    public CollectionAccessDetailsResponseModel(Collection collection, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users)
        : base(collection, "collectionAccessDetails")
    {
        Groups = groups.Select(g => new SelectionReadOnlyResponseModel(g));
        Users = users.Select(g => new SelectionReadOnlyResponseModel(g));
    }

    public CollectionAccessDetailsResponseModel(CollectionAdminDetails collection)
        : base(collection, "collectionAccessDetails")
    {
        Assigned = collection.Assigned;
        ReadOnly = collection.ReadOnly;
        HidePasswords = collection.HidePasswords;
        Manage = collection.Manage;
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
}
