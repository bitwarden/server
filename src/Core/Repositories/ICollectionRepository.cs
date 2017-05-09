using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface ICollectionRepository : IRepository<Collection, Guid>
    {
        Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
        Task<Tuple<Collection, ICollection<Guid>>> GetByIdWithGroupsAsync(Guid id);
        Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<Collection>> GetManyByUserIdAsync(Guid userId);
        Task CreateAsync(Collection obj, IEnumerable<Guid> groupIds);
        Task ReplaceAsync(Collection obj, IEnumerable<Guid> groupIds);

    }
}
