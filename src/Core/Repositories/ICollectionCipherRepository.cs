using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories;

public interface ICollectionCipherRepository
{
    Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId);
    Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<CollectionCipher>> GetManySharedByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId);
    Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds);
    Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds);
    Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId,
        IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Returns the distinct user IDs of all confirmed organization members who have access to any of the specified
    /// collections, either directly via <c>CollectionUser</c> or via group membership.
    /// </summary>
    Task<ICollection<Guid>> GetUserIdsByCollectionIdsAsync(IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Add the specified collections to the specified ciphers. If a cipher already belongs to a requested collection,
    /// no action is taken.
    /// </summary>
    /// <remarks>
    /// This method does not perform any authorization checks.
    /// </remarks>
    Task AddCollectionsForManyCiphersAsync(Guid organizationId, IEnumerable<Guid> cipherIds, IEnumerable<Guid> collectionIds);

    /// <summary>
    /// Remove the specified collections from the specified ciphers. If a cipher does not belong to a requested collection,
    /// no action is taken.
    /// </summary>
    /// <remarks>
    /// This method does not perform any authorization checks.
    /// </remarks>
    Task RemoveCollectionsForManyCiphersAsync(Guid organizationId, IEnumerable<Guid> cipherIds, IEnumerable<Guid> collectionIds);
}
