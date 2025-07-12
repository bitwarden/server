using AutoMapper;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Repositories;

public class OrganizationSubscriptionUpdateRepository(
    IServiceScopeFactory serviceScopeFactory,
    IMapper mapper)
    : Repository<OrganizationSubscriptionUpdate, Models.OrganizationSubscriptionUpdate, Guid>(
            serviceScopeFactory,
            mapper,
            context => context.OrganizationSubscriptionUpdates),
        IOrganizationSubscriptionUpdateRepository
{
    public async Task SetToUpdateSubscriptionAsync(Guid organizationId, DateTime seatsUpdatedAt)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        await using var dbContext = GetDatabaseContext(scope);

        if (dbContext.OrganizationSubscriptionUpdates.Any(osu => osu.OrganizationId == organizationId))
        {
            await dbContext.OrganizationSubscriptionUpdates
                .Where(osu => osu.OrganizationId == organizationId)
                .ExecuteUpdateAsync(osu => osu
                    .SetProperty(p => p.SeatsLastUpdated, seatsUpdatedAt)
                    .SetProperty(p => p.SyncAttempts, 0));
        }
        else
        {
            var update = new Models.OrganizationSubscriptionUpdate
            {
                OrganizationId = organizationId,
                SeatsLastUpdated = seatsUpdatedAt,
                SyncAttempts = 0
            };
            update.SetNewId();

            dbContext.OrganizationSubscriptionUpdates.Add(update);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<OrganizationSubscriptionUpdate>> GetUpdatesToSubscriptionAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        await using var dbContext = GetDatabaseContext(scope);

        return await dbContext.OrganizationSubscriptionUpdates
            .Where(osu => osu.SeatsLastUpdated != null)
            .ToListAsync();
    }

    public async Task UpdateSubscriptionStatusAsync(IEnumerable<Guid> successfulOrganizations,
        IEnumerable<Guid> failedOrganizations)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        await using var dbContext = GetDatabaseContext(scope);

        await dbContext.OrganizationSubscriptionUpdates
            .Where(osu => successfulOrganizations.Contains(osu.OrganizationId))
            .ExecuteUpdateAsync(osu => osu
                .SetProperty(x => x.SeatsLastUpdated, (DateTime?)null)
                .SetProperty(x => x.SyncAttempts, 0));

        await dbContext.OrganizationSubscriptionUpdates
            .Where(osu => failedOrganizations.Contains(osu.OrganizationId))
            .ExecuteUpdateAsync(osu => osu
                .SetProperty(x => x.SyncAttempts, x => x.SyncAttempts + 1));
    }
}
