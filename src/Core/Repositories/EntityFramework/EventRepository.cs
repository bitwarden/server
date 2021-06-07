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

        public async Task CreateAsync(IEvent e)
        {
            if (!(e is Event ev))
            {
                ev = new Event(e);
            }

            await base.CreateAsync(ev);
        }

        public async Task CreateManyAsync(IList<IEvent> entities)
        {
            if (!entities?.Any() ?? true)
            {
                return;
            }

            if (entities.Count == 1)
            {
                await CreateAsync(entities.First());
                return;
            }

            using (var scope = ServiceScopeFactory.CreateScope())
            {
                var events = entities.Select(e => e is Event ? e as Event : new Event(e));
                // TODO: solve Bulk Copy
            }
        }

        public Task CreateManyAsync(IEnumerable<IEvent> e)
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

        Task IEventRepository.CreateAsync(IEvent e)
        {
            throw new NotImplementedException();
        }
    }
}
