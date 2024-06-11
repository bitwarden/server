using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<int> GetCountByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns a collection and fetches group/user associations for the collection.
    /// </summary>
    Task<Tuple<Collection, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id);

    /// <summary>
    /// Returns a collection with permission details for the provided userId and fetches group/user associations for
    /// the collection.
    /// If the user does not have a relationship with the collection, nothing is returned.
    /// </summary>
    Task<Tuple<CollectionDetails, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id, Guid userId, bool useFlexibleCollections);

    /// <summary>
    /// Return all collections that belong to the organization. Does not include any permission details or group/user
    /// access relationships.
    /// </summary>
    Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Return all collections that belong to the organization. Includes group/user access relationships for each collection.
    /// </summary>
    Task<ICollection<Tuple<Collection, CollectionAccessDetails>>> GetManyByOrganizationIdWithAccessAsync(Guid organizationId);

    /// <summary>
    /// Returns collections that both, belong to the organization AND have an access relationship with the provided user.
    /// Includes permission details for the provided user and group/user access relationships for each collection.
    /// </summary>
    Task<ICollection<Tuple<CollectionDetails, CollectionAccessDetails>>> GetManyByUserIdWithAccessAsync(Guid userId, Guid organizationId, bool useFlexibleCollections);

    /// <summary>
    /// Returns a collection with permission details for the provided userId. Does not include group/user access
    /// relationships.
    /// If the user does not have a relationship with the collection, nothing is returned.
    /// </summary>
    Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId, bool useFlexibleCollections);
    Task<ICollection<Collection>> GetManyByManyIdsAsync(IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Return all collections a user has access to across all of the organization they're a member of. Includes permission
    /// details for each collection.
    /// </summary>
    Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId, bool useFlexibleCollections);

    /// <summary>
    /// Returns all collections for an organization, including permission info for the specified user.
    /// This does not perform any authorization checks internally!
    /// Optionally, you can include access relationships for other Groups/Users and the collections.
    /// </summary>
    Task<ICollection<CollectionAdminDetails>> GetManyByOrganizationIdWithPermissionsAsync(Guid organizationId, Guid userId, bool includeAccessRelationships);

    /// <summary>
    /// Returns the collection by Id, including permission info for the specified user.
    /// This does not perform any authorization checks internally!
    /// Optionally, you can include access relationships for other Groups/Users and the collection.
    /// </summary>
    Task<CollectionAdminDetails> GetByIdWithPermissionsAsync(Guid collectionId, Guid? userId, bool includeAccessRelationships);

    Task CreateAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users);
    Task ReplaceAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users);
    Task DeleteUserAsync(Guid collectionId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid id, IEnumerable<CollectionAccessSelection> users);
    Task<ICollection<CollectionAccessSelection>> GetManyUsersByIdAsync(Guid id);
    Task DeleteManyAsync(IEnumerable<Guid> collectionIds);
    Task CreateOrUpdateAccessForManyAsync(Guid organizationId, IEnumerable<Guid> collectionIds,
        IEnumerable<CollectionAccessSelection> users, IEnumerable<CollectionAccessSelection> groups);
}
