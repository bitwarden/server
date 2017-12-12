using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IEventRepository
    {
        Task<ICollection<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task CreateAsync(IEvent e);
        Task CreateManyAsync(IList<IEvent> e);
    }
}
