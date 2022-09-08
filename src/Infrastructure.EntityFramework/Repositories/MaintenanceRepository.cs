using AutoMapper;
using Bit.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class MaintenanceRepository : BaseEntityFrameworkRepository, IMaintenanceRepository
{
    public MaintenanceRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task DeleteExpiredGrantsAsync()
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from g in dbContext.Grants
                        where g.ExpirationDate < DateTime.UtcNow
                        select g;
            dbContext.RemoveRange(query);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task DeleteExpiredSponsorshipsAsync(DateTime validUntilBeforeDate)
    {
        using (var scope = ServiceScopeFactory.CreateScope())
        {
            var dbContext = GetDatabaseContext(scope);
            var query = from s in dbContext.OrganizationSponsorships
                        where s.ValidUntil < validUntilBeforeDate
                        select s;
            dbContext.RemoveRange(query);
            await dbContext.SaveChangesAsync();
        }
    }

    public Task DisableCipherAutoStatsAsync()
    {
        return Task.CompletedTask;
    }

    public Task RebuildIndexesAsync()
    {
        return Task.CompletedTask;
    }

    public Task UpdateStatisticsAsync()
    {
        return Task.CompletedTask;
    }
}
