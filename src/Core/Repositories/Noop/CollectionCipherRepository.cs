using Bit.Core.Entities;

#nullable enable

namespace Bit.Core.Repositories.Noop;

public class CollectionCipherRepository : ICollectionCipherRepository
{
    public Task<ICollection<CollectionCipher>> GetManyByUserIdAsync(Guid userId)
        => Task.FromResult<ICollection<CollectionCipher>>([]);

    public Task<ICollection<CollectionCipher>> GetManyByOrganizationIdAsync(Guid organizationId)
        => Task.FromResult<ICollection<CollectionCipher>>([]);

    public Task<ICollection<CollectionCipher>> GetManySharedByOrganizationIdAsync(Guid organizationId)
        => Task.FromResult<ICollection<CollectionCipher>>([]);

    public Task<ICollection<CollectionCipher>> GetManyByUserIdCipherIdAsync(Guid userId, Guid cipherId)
        => Task.FromResult<ICollection<CollectionCipher>>([]);

    public Task UpdateCollectionsAsync(Guid cipherId, Guid userId, IEnumerable<Guid> collectionIds)
        => Task.CompletedTask;

    public Task UpdateCollectionsForAdminAsync(Guid cipherId, Guid organizationId, IEnumerable<Guid> collectionIds)
        => Task.CompletedTask;

    public Task UpdateCollectionsForCiphersAsync(IEnumerable<Guid> cipherIds, Guid userId, Guid organizationId, IEnumerable<Guid> collectionIds)
        => Task.CompletedTask;

    public Task<ICollection<Guid>> GetUserIdsByCollectionIdsAsync(IEnumerable<Guid> collectionIds)
        => Task.FromResult<ICollection<Guid>>([]);

    public Task AddCollectionsForManyCiphersAsync(Guid organizationId, IEnumerable<Guid> cipherIds, IEnumerable<Guid> collectionIds)
        => Task.CompletedTask;

    public Task RemoveCollectionsForManyCiphersAsync(Guid organizationId, IEnumerable<Guid> cipherIds, IEnumerable<Guid> collectionIds)
        => Task.CompletedTask;
}
