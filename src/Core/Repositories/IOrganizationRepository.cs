using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Domains;

namespace Bit.Core.Repositories
{
    public interface IOrganizationRepository : IRepository<Organization, Guid>
    {
        Task<Organization> GetByIdAsync(Guid id, Guid userId);
        Task<ICollection<Organization>> GetManyByUserIdAsync(Guid userId);
    }
}
