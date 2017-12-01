using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Microsoft.WindowsAzure.Storage.Table;

namespace Bit.Core.Repositories
{
    public interface IEventRepository
    {
        Task<ICollection<EventTableEntity>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate);
        Task CreateAsync(ITableEntity entity);
        Task CreateManyAsync(IEnumerable<ITableEntity> entities);
    }
}
