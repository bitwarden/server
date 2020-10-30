using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Bit.Core.Repositories
{
    public interface ISendRepository : IRepository<Send, Guid>
    {
        Task<ICollection<Send>> GetManyByUserIdAsync(Guid userId);
        Task<ICollection<Send>> GetManyByDeletionDateAsync(DateTime deletionDateBefore);
    }
}
