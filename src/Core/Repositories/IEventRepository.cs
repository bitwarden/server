using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IEventRepository
    {
        Task<ICollection<EventTableEntity>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task CreateAsync(EventTableEntity entity);
        Task CreateManyAsync(IList<EventTableEntity> entities);
    }
}
