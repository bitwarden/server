using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface ICollectionRepository : IRepository<Collection, Guid>
    {
        Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
        Task<Tuple<Collection, ICollection<SelectionReadOnly>>> GetByIdWithGroupsAsync(Guid id);
        Task<ICollection<Collection>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<CollectionDetails>> GetManyByUserIdAsync(Guid userId);
        Task<ICollection<CollectionUserDetails>> GetManyUserDetailsByIdAsync(Guid organizationId, Guid collectionId);
        Task CreateAsync(Collection obj, IEnumerable<SelectionReadOnly> groups);
        Task ReplaceAsync(Collection obj, IEnumerable<SelectionReadOnly> groups);
        Task DeleteUserAsync(Guid collectionId, Guid organizationUserId);
    }
}
