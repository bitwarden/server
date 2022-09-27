using AutoMapper;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.Infrastructure.EntityFramework.Repositories.Queries;
using LinqToDB.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Cipher = Bit.Core.Entities.Cipher;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class EventRepository : Repository<Core.Entities.Event, Event, Guid>, IEventRepository
{
    public EventRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, (DatabaseContext context) => context.Events)
    { }

    public async Task CreateAsync(IEvent e)
    {
        if (e is not Core.Entities.Event ev)
        {
            ev = new Core.Entities.Event(e);
        }

        await base.CreateAsync(ev);
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> entities)
    {
        if (!entities?.Any() ?? true)
        {
            return;
        }

        if (!entities.Skip(1).Any())
        {
            await CreateAsync(entities.First());
            return;
        }

        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var tableEvents = entities.Select(e => e as Core.Entities.Event ?? new Core.Entities.Event(e));
            var entityEvents = Mapper.Map<List<Event>>(tableEvents);
            entityEvents.ForEach(e => e.SetNewId());
            await dbContext.BulkCopyAsync(entityEvents);
        }
    }

    public async Task<PagedResult<IEvent>> GetManyByCipherAsync(Cipher cipher, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByCipherIdQuery(cipher, startDate, endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }


    public async Task<PagedResult<IEvent>> GetManyByOrganizationActingUserAsync(Guid organizationId, Guid actingUserId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByOrganizationIdActingUserIdQuery(organizationId, actingUserId,
                startDate, endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderAsync(Guid providerId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByProviderIdQuery(providerId, startDate,
                endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }

    public async Task<PagedResult<IEvent>> GetManyByProviderActingUserAsync(Guid providerId, Guid actingUserId,
        DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByProviderIdActingUserIdQuery(providerId, actingUserId,
                startDate, endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }

    public async Task<PagedResult<IEvent>> GetManyByOrganizationAsync(Guid organizationId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByOrganizationIdQuery(organizationId, startDate,
                endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }

    public async Task<PagedResult<IEvent>> GetManyByUserAsync(Guid userId, DateTime startDate, DateTime endDate, PageOptions pageOptions)
    {
        DateTime? beforeDate = null;
        if (!string.IsNullOrWhiteSpace(pageOptions.ContinuationToken) &&
            long.TryParse(pageOptions.ContinuationToken, out var binaryDate))
        {
            beforeDate = DateTime.SpecifyKind(DateTime.FromBinary(binaryDate), DateTimeKind.Utc);
        }
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = new EventReadPageByUserIdQuery(userId, startDate,
                endDate, beforeDate, pageOptions);
            var events = await query.Run(dbContext).ToListAsync();

            var result = new PagedResult<IEvent>();
            if (events.Any() && events.Count >= pageOptions.PageSize)
            {
                result.ContinuationToken = events.Last().Date.ToBinary().ToString();
            }
            result.Data.AddRange(events);
            return result;
        }
    }
}
