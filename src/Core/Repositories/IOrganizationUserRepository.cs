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
        Task<ICollection<OrganizationUser>> GetManyByUserAsync(Guid userId);
        Task<ICollection<OrganizationUser>> GetManyByOrganizationAsync(Guid organizationId, OrganizationUserType? type);
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, string email);
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId);
        Task<Tuple<OrganizationUserUserDetails, ICollection<CollectionUserCollectionDetails>>> GetDetailsByIdAsync(Guid id);
        Task<ICollection<OrganizationUserUserDetails>> GetManyDetailsByOrganizationAsync(Guid organizationId);
        Task<ICollection<OrganizationUserOrganizationDetails>> GetManyDetailsByUserAsync(Guid userId,
            OrganizationUserStatusType? status = null);
    }
}
