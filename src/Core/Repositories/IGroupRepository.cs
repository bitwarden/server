using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IGroupRepository : IRepository<Group, Guid>
    {
        Task<Tuple<Group, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id);
        Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId);
        Task<ICollection<Guid>> GetManyUserIdsByIdAsync(Guid id);
        Task<ICollection<GroupUser>> GetManyGroupUsersByOrganizationIdAsync(Guid organizationId);
        Task CreateAsync(Group obj, IEnumerable<SelectionReadOnly> collections);
        Task ReplaceAsync(Group obj, IEnumerable<SelectionReadOnly> collections);
        Task DeleteUserAsync(Guid groupId, Guid organizationUserId);
        Task UpdateUsersAsync(Guid groupId, IEnumerable<Guid> organizationUserIds);
    }
}
