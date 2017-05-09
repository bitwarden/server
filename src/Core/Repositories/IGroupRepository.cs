using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IGroupRepository : IRepository<Group, Guid>
    {
        Task<Tuple<Group, ICollection<Guid>>> GetByIdWithCollectionsAsync(Guid id);
        Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<GroupUserUserDetails>> GetManyUserDetailsByIdAsync(Guid id);
        Task<ICollection<Guid>> GetManyIdsByUserIdAsync(Guid organizationUserId);
        Task CreateAsync(Group obj, IEnumerable<Guid> collectionIds);
        Task ReplaceAsync(Group obj, IEnumerable<Guid> collectionIds);
    }
}
