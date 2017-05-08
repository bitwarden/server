using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bit.Core.Repositories
{
    public interface IGroupRepository : IRepository<Group, Guid>
    {
        Task<ICollection<Group>> GetManyByOrganizationIdAsync(Guid organizationId);
    }
}
