using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface ISubvaultRepository : IRepository<Subvault, Guid>
    {
        Task<int> GetCountByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<Subvault>> GetManyByOrganizationIdAsync(Guid organizationId);
        Task<ICollection<Subvault>> GetManyByUserIdAsync(Guid userId);

    }
}
