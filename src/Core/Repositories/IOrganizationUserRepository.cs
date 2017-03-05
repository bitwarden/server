using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IOrganizationUserRepository : IRepository<OrganizationUser, Guid>
    {
        Task<OrganizationUser> GetByOrganizationAsync(Guid organizationId, Guid userId);
        Task<OrganizationUserDetails> GetDetailsByIdAsync(Guid id);
        Task<ICollection<OrganizationUserDetails>> GetManyDetailsByOrganizationsAsync(Guid organizationId);
    }
}
