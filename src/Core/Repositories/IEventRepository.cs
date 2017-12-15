using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;

namespace Bit.Core.Repositories
{
    public interface IEventRepository
    {
        Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate,
            PageOptions pageOptions);
        Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId, DateTime startDate, DateTime endDate,
            PageOptions pageOptions);
        Task CreateAsync(IEvent e);
        Task CreateManyAsync(IList<IEvent> e);
    }
}
