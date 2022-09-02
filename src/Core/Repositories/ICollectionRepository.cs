using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface ICollectionRepository : IRepository<Collection, Guid>
{
    Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
    Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id);
    Task<Tuple<CollectionDetails, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id, Guid userId);
    Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<CollectionDetails> GetByIdAsync(Guid id, Guid userId);
    Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId);
    Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups);
    Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups);
    Task DeleteUserAsync(Guid collectionId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid id, IEnumerable<SelectionReadOnly> users);
    Task<ICollection<SelectionReadOnly>> GetManyUsersByIdAsync(Guid id);
}
