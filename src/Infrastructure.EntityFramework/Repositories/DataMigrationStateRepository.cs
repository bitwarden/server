#nullable enable

using AutoMapper;
using Bit.Core.Jobs.DataMigrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.EntityFramework.Repositories;

public class DataMigrationStateRepository : BaseEntityFrameworkRepository, IDataMigrationStateRepository
{
    private readonly TimeProvider _timeProvider;

    public DataMigrationStateRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper,
        TimeProvider timeProvider)
        : base(serviceScopeFactory, mapper)
    {
        _timeProvider = timeProvider;
    }

    public async Task<bool> ExistsAsync(string name, CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.DataMigrationStates.AnyAsync(s => s.Name == name, token);
    }

    public async Task InitializeAsync(string name, IReadOnlyList<PartitionRange> partitions,
        CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        dbContext.DataMigrationStates.AddRange(partitions.Select(p =>
        {
            var state = new Models.DataMigrationState
            {
                Name = name,
                Partition = p.Partition,
                RangeStart = p.RangeStart,
                RangeEnd = p.RangeEnd,
                TotalRows = p.TotalRows,
                CreationDate = now,
                RevisionDate = now,
            };
            state.SetNewId();
            return state;
        }));
        try
        {
            await dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException)
        {
            // Unique (Name, Partition) violation: another instance won the initialization race.
            // Its boundary set stands; ours failed whole.
        }
    }

    public async Task<PartitionClaim?> TryClaimAsync(string name, string owner, TimeSpan leaseDuration,
        CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Optimistic claim loop: list available partitions, then race for one at a time with a
        // conditional UPDATE. Affected == 0 means another claimer got there first; move on.
        var candidates = await dbContext.DataMigrationStates.AsNoTracking()
            .Where(s => s.Name == name && s.CompletedDate == null &&
                        (s.LeaseOwner == null || s.LeaseExpiresDate < now))
            .Select(s => s.Partition)
            .ToListAsync(token);

        foreach (var partition in candidates)
        {
            var claimed = await dbContext.DataMigrationStates
                .Where(s => s.Name == name && s.Partition == partition && s.CompletedDate == null &&
                            (s.LeaseOwner == null || s.LeaseExpiresDate < now))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LeaseOwner, owner)
                    .SetProperty(s => s.LeaseExpiresDate, now.Add(leaseDuration))
                    .SetProperty(s => s.RevisionDate, now), token);
            if (claimed != 1)
            {
                continue;
            }

            var row = await dbContext.DataMigrationStates.AsNoTracking()
                .FirstAsync(s => s.Name == name && s.Partition == partition, token);
            return new PartitionClaim(row.Partition, row.RangeStart, row.RangeEnd, row.Cursor,
                row.TotalRows, row.RowsScanned, row.RowsConverted, row.RowsSkippedByRace,
                row.RowsFailed, row.StartedDate);
        }

        return null;
    }

    public async Task<bool> CheckpointAsync(string name, int partition, string owner,
        MigrationCheckpoint checkpoint, TimeSpan leaseDuration, CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Fenced by owner, exactly like the stored-procedure track. Renews the lease: renewal
        // rides the per-batch checkpoint, so a stalled worker stops renewing and expires.
        var affected = await dbContext.DataMigrationStates
            .Where(s => s.Name == name && s.Partition == partition && s.LeaseOwner == owner)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LeaseExpiresDate, now.Add(leaseDuration))
                .SetProperty(s => s.Cursor, checkpoint.Cursor)
                .SetProperty(s => s.RowsScanned, checkpoint.RowsScanned)
                .SetProperty(s => s.RowsConverted, checkpoint.RowsConverted)
                .SetProperty(s => s.RowsSkippedByRace, checkpoint.RowsSkippedByRace)
                .SetProperty(s => s.RowsFailed, checkpoint.RowsFailed)
                .SetProperty(s => s.StartedDate, checkpoint.StartedDate)
                .SetProperty(s => s.CompletedDate, checkpoint.CompletedDate)
                .SetProperty(s => s.RevisionDate, now), token);
        return affected == 1;
    }

    public async Task ReleaseAsync(string name, int partition, string owner, CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.DataMigrationStates
            .Where(s => s.Name == name && s.Partition == partition && s.LeaseOwner == owner)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LeaseOwner, (string?)null)
                .SetProperty(s => s.LeaseExpiresDate, (DateTime?)null)
                .SetProperty(s => s.RevisionDate, now), token);
    }

    public async Task<IReadOnlyList<PartitionProgress>> ReadProgressAsync(string name,
        CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Plain read, no lease predicate: the pending-rows tally must see leased partitions too.
        return await dbContext.DataMigrationStates.AsNoTracking()
            .Where(s => s.Name == name)
            .OrderBy(s => s.Partition)
            .Select(s => new PartitionProgress(
                s.Partition, s.TotalRows, s.RowsScanned, s.CompletedDate != null))
            .ToListAsync(token);
    }

    public async Task<int> ReadIncompleteCountAsync(string name, CancellationToken token)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.DataMigrationStates
            .CountAsync(s => s.Name == name && s.CompletedDate == null, token);
    }
}
