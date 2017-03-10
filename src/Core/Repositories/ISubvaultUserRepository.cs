using System;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface ISubvaultUserRepository : IRepository<SubvaultUser, Guid>
    {
        Task<ICollection<SubvaultUser>> GetManyByOrganizationUserIdAsync(Guid orgUserId);
    }
}
