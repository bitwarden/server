using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DataModel = Bit.Core.Models.Data;
using EfModel = Bit.Core.Models.EntityFramework;
using TableModel = Bit.Core.Models.Table;

namespace Bit.Core.Repositories.EntityFramework
{
    public class EventRepository : Repository<TableModel.Event, EfModel.Event, Guid>, IEventRepository
    {
        public EventRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
            : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Events)
        { }

        public async Task<IEvent> CreateAsync(IEvent e)
        {
            if (!(e is Event ev))
            {
                ev = new Event(e);
            }

            return await base.CreateAsync(ev);
        }

        public Task CreateManyAsync(IList<IEvent> e)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            throw new NotImplementedException();
        }

        public Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
        {
            throw new NotImplementedException();
        }
    }
}
