#nullable enable

using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrganizationEventCleanup = Bit.Core.Dirt.Entities.OrganizationEventCleanup;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationEventCleanupRepository :
    Repository<OrganizationEventCleanup, Dirt.Models.OrganizationEventCleanup, Guid>,
    IOrganizationEventCleanupRepository
{
    public OrganizationEventCleanupRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.OrganizationEventCleanups)
    {
    }

    async Task IOrganizationEventCleanupRepository.CreateAsync(OrganizationEventCleanup cleanup)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = Mapper.Map<Dirt.Models.OrganizationEventCleanup>(cleanup);
        await dbContext.OrganizationEventCleanups.AddAsync(entity);
        await dbContext.SaveChangesAsync();
    }

    public async Task<OrganizationEventCleanup?> GetNextPendingAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await dbContext.OrganizationEventCleanups
            .Where(c => c.CompletedAt == null)
            .OrderBy(c => c.QueuedAt)
            .FirstOrDefaultAsync();
        return entity is null ? null : Mapper.Map<OrganizationEventCleanup>(entity);
    }

    public async Task MarkStartedAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;
        await dbContext.OrganizationEventCleanups
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.StartedAt, c => c.StartedAt ?? now)
                .SetProperty(c => c.LastProgressAt, now));
    }

    public async Task IncrementProgressAsync(Guid id, long delta)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;
        await dbContext.OrganizationEventCleanups
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.EventsDeletedCount, c => c.EventsDeletedCount + delta)
                .SetProperty(c => c.LastProgressAt, now));
    }

    public async Task MarkCompletedAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;
        await dbContext.OrganizationEventCleanups
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.CompletedAt, now)
                .SetProperty(c => c.LastProgressAt, now));
    }

    public async Task RecordErrorAsync(Guid id, string message)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;
        await dbContext.OrganizationEventCleanups
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Attempts, c => c.Attempts + 1)
                .SetProperty(c => c.LastError, message)
                .SetProperty(c => c.LastProgressAt, now));
    }
}
