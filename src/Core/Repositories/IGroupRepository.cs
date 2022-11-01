using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IGroupRepository : IRepository<Group, Guid>
{
    Task<Tuple<Group, ICollection<CollectionAccessSelection>>> GetByIdWithCollectionsAsync(Guid id);
    Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId);
    Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id);
    Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId);
    Task CreateAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    Task ReplaceAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    Task DeleteUserAsync(Guid groupId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds);
}
