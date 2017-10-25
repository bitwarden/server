using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;
using Bit.Core.Enums;

namespace Bit.Core.Repositories
{
    public interface IOrganizationUserRepository : IRepository<OrganizationUser, Guid>
    {
        Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
        Task<int> GetCountByFreeOrganizationAdminUserAsync(Guid userId);
        Task<int> GetCountByOnlyOwnerAsync(Guid userId);
        Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId);
        Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type);
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, string email);
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId);
        Task<Tuple<OrganizationUser, ICollection<SelectionReadOnly>>> GetByIdWithCollectionsAsync(Guid id);
        Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId);
        Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
            OrganizationUserStatusType? status = null);
        Task UpdateGroupsAsync(Guid orgUserId, IEnumerable<Guid> groupIds);
        Task CreateAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections);
        Task ReplaceAsync(OrganizationUser obj, IEnumerable<SelectionReadOnly> collections);
    }
}
