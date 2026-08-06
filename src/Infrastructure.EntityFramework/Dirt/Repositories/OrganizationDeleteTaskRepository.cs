using AutoMapper;
using Bit.Core.Dirt.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Core.Dirt.Entities.OrganizationDeleteTask;
using EfModel = Bit.Infrastructure.EntityFramework.Dirt.Models.OrganizationDeleteTask;

namespace Bit.Infrastructure.EntityFramework.Dirt.Repositories;

public class OrganizationDeleteTaskRepository : BaseEntityFrameworkRepository, IOrganizationDeleteTaskRepository
{
    /// <summary>
    /// How many times a claim may lose its race before reporting "nothing to claim". Bounded so a
    /// contended queue cannot spin here indefinitely; the job simply tries again on its next run.
    /// </summary>
    private const int MaxClaimAttempts = 3;

    public OrganizationDeleteTaskRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task CreateAsync(CoreEntity task)
    {
        task.SetNewId();

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await dbContext.OrganizationDeleteTasks.AddAsync(Mapper.Map<EfModel>(task));
        await dbContext.SaveChangesAsync();
    }

    public async Task<CoreEntity?> ClaimNextPendingAsync()
    {
        // The MSSQL procedure claims in one statement using WITH (UPDLOCK, READPAST), which has no
        // EF Core translation. This uses optimistic concurrency to the same effect: pick the oldest
        // claimable task, then update it only if it is *still* claimable. A worker that loses the
        // race updates zero rows and moves on to the next candidate, so a row is never claimed twice.
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        for (var attempt = 0; attempt < MaxClaimAttempts; attempt++)
        {
            var now = DateTime.UtcNow;
            var staleLeaseThreshold = now.AddMinutes(-CoreEntity.LeaseDurationMinutes);

            var candidateId = await dbContext.OrganizationDeleteTasks
                .Where(t => t.CompletedDate == null
                            && (t.StartDate == null || t.RevisionDate < staleLeaseThreshold)
                            && t.FailureCount < CoreEntity.MaxFailureCount)
                .OrderBy(t => t.CreationDate)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync();

            if (candidateId is null)
            {
                return null;
            }

            // Repeating the claimable predicate is what makes this safe: if another worker claimed
            // the row after the read above, this matches nothing.
            var claimed = await dbContext.OrganizationDeleteTasks
                .Where(t => t.Id == candidateId
                            && t.CompletedDate == null
                            && (t.StartDate == null || t.RevisionDate < staleLeaseThreshold))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(t => t.StartDate, t => t.StartDate ?? now)
                    .SetProperty(t => t.RevisionDate, now));

            if (claimed == 0)
            {
                continue;
            }

            var task = await dbContext.OrganizationDeleteTasks
                .AsNoTracking()
                .FirstAsync(t => t.Id == candidateId);

            return Mapper.Map<CoreEntity>(task);
        }

        return null;
    }

    public async Task UpdateProgressAsync(Guid id, long delta)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;

        await dbContext.OrganizationDeleteTasks
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.ItemsDeletedCount, t => t.ItemsDeletedCount + delta)
                .SetProperty(t => t.RevisionDate, now));
    }

    public async Task<int> UpdateErrorAsync(Guid id, string message)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;

        await dbContext.OrganizationDeleteTasks
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.FailureCount, t => t.FailureCount + 1)
                .SetProperty(t => t.LastError, message)
                .SetProperty(t => t.RevisionDate, now));

        // ExecuteUpdate reports rows affected, not the new value, so read the count back. This is
        // the failure path, so the extra round trip is not worth avoiding.
        return await dbContext.OrganizationDeleteTasks
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => t.FailureCount)
            .FirstOrDefaultAsync();
    }

    public async Task UpdateCompletedAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;

        await dbContext.OrganizationDeleteTasks
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.CompletedDate, now)
                .SetProperty(t => t.RevisionDate, now));
    }
}
