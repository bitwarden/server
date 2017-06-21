using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Repositories
{
    public interface IU2fRepository : IRepository<U2f, int>
    {
        Task<ICollection<U2f>> GetManyByUserIdAsync(Guid userId);
        Task DeleteManyByUserIdAsync(Guid userId);
    }
}
