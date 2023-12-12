using Bit.Core.Entities;

namespace Bit.Core.Repositories;

public interface ICollectionCipherRepository
{
    Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId, bool useFlexibleCollections);
    Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId, bool useFlexibleCollections);
    Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds, bool useFlexibleCollections);
    Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds);
    Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId,
        IEnumerable<Guid> collectionIds, bool useFlexibleCollections);
}
