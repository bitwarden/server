using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories;

public interface IGroupRepository : IRepository<Group, Guid>
{
    Task<(Group group, ICollection<CollectionAccessSelection> accessSelection)> GetByIdWithCollectionsAsync(Guid id);
    Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ICollection<Tuple<Group, ICollection<CollectionAccessSelection>>>> GetManyWithCollectionsByOrganizationIdAsync(
        Guid organizationId);
    Task<ICollection<Group>> GetManyByManyIds(IEnumerable<Guid> groupIds);
    Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId);
    Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id);
    Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId);
    Task<GroupUser> GetGroupUserByGroupIdOrganizationUserId(Guid id, Guid orgUserId);
    Task CreateAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    Task ReplaceAsync(Group obj, IEnumerable<CollectionAccessSelection> collections);
    Task DeleteUserAsync(Guid groupId, Guid organizationUserId);
    Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds);
    Task DeleteManyAsync(IEnumerable<Guid> groupIds);
}
