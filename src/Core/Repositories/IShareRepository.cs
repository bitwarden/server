using System;
using Bit.Core.Domains;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface IShareRepository : IRepository<Share, Guid>
    {
        Task<Share> GetByIdAsync(Guid id, Guid userId);
        Task<ICollection<Share>> GetManyByCipherId(Guid id);
    }
}
