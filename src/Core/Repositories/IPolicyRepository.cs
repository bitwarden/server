using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IPolicyRepository : IRepository<Policy, Guid>
    {
        Task<ICollection<Policy>> GetManyByOrganizationIdAsync(Guid organizationId);
    }
}
