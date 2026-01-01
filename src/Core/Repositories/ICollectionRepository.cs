using Bit.Core.Entities;
using Bit.Core.Models.Data;

#nullable enable

namespace Bit.Core.Repositories;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<int> GetCountByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Returns a collection and fetches group/user associations for the collection.
    /// </summary>
    Task<Tuple<Collection?, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id);

    /// <summary>
    /// Return all collections that belong to the organization. Does not include any permission details or group/user
    /// access relationships.
    /// </summary>
    Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <inheritdoc cref="GetManyByOrganizationIdAsync"/>
    /// <remarks>
    /// Excludes default collections (My Items collections) - used by Admin Console Collections tab.
    /// </remarks>
    Task<ICollection<Collection>> GetManySharedCollectionsByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Return all shared collections that belong to the organization. Includes group/user access relationships for each collection.
    /// </summary>
    Task<ICollection<Tuple<Collection, CollectionAccessDetails>>> GetManyByOrganizationIdWithAccessAsync(Guid organizationId);

    Task<ICollection<Collection>> GetManyByManyIdsAsync(IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Return all collections a user has access to across all of the organization they're a member of. Includes permission
    /// details for each collection.
    /// </summary>
    Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId);

    /// <summary>
    /// Returns all shared collections for an organization, including permission info for the specified user.
    /// This does not perform any authorization checks internally!
    /// Optionally, you can include access relationships for other Groups/Users and the collections.
    /// Excludes default collections (My Items collections) - used by Admin Console Collections tab.
    /// </summary>
    Task<ICollection<CollectionAdminDetails>> GetManyByOrganizationIdWithPermissionsAsync(Guid organizationId, Guid userId, bool includeAccessRelationships);

    /// <summary>
    /// Returns the collection by Id, including permission info for the specified user.
    /// This does not perform any authorization checks internally!
    /// Optionally, you can include access relationships for other Groups/Users and the collection.
    /// </summary>
    Task<CollectionAdminDetails?> GetByIdWithPermissionsAsync(Guid collectionId, Guid? userId, bool includeAccessRelationships);

    Task CreateAsync(Collection obj, IEnumerable<CollectionAccessSelection>? groups, IEnumerable<CollectionAccessSelection>? users);
    Task ReplaceAsync(Collection obj, IEnumerable<CollectionAccessSelection>? groups, IEnumerable<CollectionAccessSelection>? users);
    Task DeleteUserAsync(Guid collectionId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid id, IEnumerable<CollectionAccessSelection> users);
    Task<ICollection<CollectionAccessSelection>> GetManyUsersByIdAsync(Guid id);
    Task DeleteManyAsync(IEnumerable<Guid> collectionIds);
    Task CreateOrUpdateAccessForManyAsync(Guid organizationId, IEnumerable<Guid> collectionIds,
        IEnumerable<CollectionAccessSelection> users, IEnumerable<CollectionAccessSelection> groups);

    /// <summary>
    /// Creates default user collections for the specified organization users.
    /// Throws an exception if any user already has a default collection for the organization.
    /// </summary>
    /// <param name="organizationId">The Organization ID.</param>
    /// <param name="organizationUserIds">The Organization User IDs to create default collections for.</param>
    /// <param name="defaultCollectionName">The encrypted string to use as the default collection name.</param>
    Task CreateDefaultCollectionsAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, string defaultCollectionName);

    /// <summary>
    /// Creates default user collections for the specified organization users using bulk insert operations.
    /// Use this if you need to create collections for > ~1k users.
    /// Throws an exception if any user already has a default collection for the organization.
    /// </summary>
    /// <param name="organizationId">The Organization ID.</param>
    /// <param name="organizationUserIds">The Organization User IDs to create default collections for.</param>
    /// <param name="defaultCollectionName">The encrypted string to use as the default collection name.</param>
    /// <remarks>
    /// If any of the OrganizationUsers may already have default collections, the caller should first filter out these
    /// users using GetDefaultCollectionSemaphoresAsync before calling this method.
    /// </remarks>
    Task CreateDefaultCollectionsBulkAsync(Guid organizationId, IEnumerable<Guid> organizationUserIds, string defaultCollectionName);

    /// <summary>
    /// Gets default collection semaphores for the given organizationUserIds.
    /// If an organizationUserId is missing from the result set, they do not have a semaphore set.
    /// </summary>
    /// <param name="organizationUserIds">The organization User IDs to check semaphores for.</param>
    /// <returns>Collection of organization user IDs that have default collection semaphores.</returns>
    /// <remarks>
    /// The semaphore table is used to ensure that an organizationUser can only have 1 default collection.
    /// (That is, a user may only have 1 default collection per organization.)
    /// If a semaphore is returned, that user already has a default collection for that organization.
    /// </remarks>
    Task<HashSet<Guid>> GetDefaultCollectionSemaphoresAsync(IEnumerable<Guid> organizationUserIds);
}
