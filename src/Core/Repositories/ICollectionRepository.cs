using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
    Task<Tuple<Collection, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id);
    Task<Tuple<CollectionDetails, CollectionAccessDetails>> GetByIdWithAccessAsync(Guid id, Guid userId);
    Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Tuple<Collection, ICollection<CollectionAccessSelection>>>> GetManyWithGroupsByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Tuple<Collection, ICollection<CollectionAccessSelection>>>> GetManyWithGroupsByUserIdAsync(Guid userId, Guid organizationId);
    Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<Collection>> GetManyByManyIdsAsync(IEnumerable<Guid> collectionIds);
    Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId);
    Task CreateAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users);
    Task ReplaceAsync(Collection obj, IEnumerable<CollectionAccessSelection> groups, IEnumerable<CollectionAccessSelection> users);
    Task DeleteUserAsync(Guid collectionId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid id, IEnumerable<CollectionAccessSelection> users);
    Task<ICollection<CollectionAccessSelection>> GetManyUsersByIdAsync(Guid id);
    Task DeleteManyAsync(IEnumerable<Guid> collectionIds);
}
