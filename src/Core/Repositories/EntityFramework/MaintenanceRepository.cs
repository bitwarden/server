using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Repositories.EntityFramework
{
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
}
