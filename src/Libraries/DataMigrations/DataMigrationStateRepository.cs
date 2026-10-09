using Bit.Core.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.DataMigrations;

/// <summary>
/// Reads and manages the state of data migrations, one row per partition.
/// </summary>
public interface IDataMigrationStateRepository
{
    /// <summary>Every partition, ordered by migration name then partition, optionally for one migration.</summary>
    Task<IReadOnlyList<DataMigrationState>> GetManyAsync(string? name = null, CancellationToken cancellationToken = default);

    /// <summary>Pauses a migration's incomplete partitions; a run that respects pauses stops at its next checkpoint.</summary>
    Task PauseAsync(string name, CancellationToken cancellationToken = default);

    Task ResumeAsync(string name, CancellationToken cancellationToken = default);
}

internal record DataMigrationCheckpoint(string Cursor, long Scanned, long Converted, long Skipped, long Failed, bool Completed);

internal class DataMigrationStateRepository(IServiceScopeFactory scopeFactory, TimeProvider timeProvider)
    : IDataMigrationStateRepository
{
    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public Task<IReadOnlyList<DataMigrationState>> GetManyAsync(string? name = null, CancellationToken cancellationToken = default) =>
        UseAsync<IReadOnlyList<DataMigrationState>>(async db => await db.Set<DataMigrationState>().AsNoTracking()
            .Where(s => name == null || s.Name == name)
            .OrderBy(s => s.Name).ThenBy(s => s.Partition)
            .ToListAsync(cancellationToken));

    public Task PauseAsync(string name, CancellationToken cancellationToken = default)
    {
        var now = Now;
        return UseAsync(db => db.Set<DataMigrationState>()
            .Where(s => s.Name == name && s.CompletedDate == null && s.PausedDate == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.PausedDate, now).SetProperty(s => s.RevisionDate, now), cancellationToken));
    }

    public Task ResumeAsync(string name, CancellationToken cancellationToken = default)
    {
        var now = Now;
        return UseAsync(db => db.Set<DataMigrationState>()
            .Where(s => s.Name == name && s.PausedDate != null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.PausedDate, (DateTime?)null).SetProperty(s => s.RevisionDate, now), cancellationToken));
    }

    internal Task DeleteManyAsync(string name, CancellationToken cancellationToken) =>
        UseAsync(db => db.Set<DataMigrationState>().Where(s => s.Name == name).ExecuteDeleteAsync(cancellationToken));

    /// <summary>
    /// Moves the cursor of a migration's partitions that completed with failed rows back to their range start, and
    /// clears their counters, dates and lease, so they run again.
    /// </summary>
    internal Task ResetFailedAsync(string name, CancellationToken cancellationToken)
    {
        var now = Now;
        return UseAsync(db => db.Set<DataMigrationState>()
            .Where(s => s.Name == name && s.CompletedDate != null && s.RowsFailed > 0)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Cursor, s => s.RangeStart)
                .SetProperty(s => s.RowsScanned, 0L)
                .SetProperty(s => s.RowsConverted, 0L)
                .SetProperty(s => s.RowsSkippedByRace, 0L)
                .SetProperty(s => s.RowsFailed, 0L)
                .SetProperty(s => s.LeaseOwner, (string?)null)
                .SetProperty(s => s.LeaseExpiresDate, (DateTime?)null)
                .SetProperty(s => s.StartedDate, (DateTime?)null)
                .SetProperty(s => s.CompletedDate, (DateTime?)null)
                .SetProperty(s => s.RevisionDate, now), cancellationToken));
    }

    /// <summary>
    /// Inserts a migration's partitions together. If another execution inserts them first, these are rejected, by the
    /// unique (Name, Partition) index or as a deadlock victim, and the existing partitions are returned instead.
    /// </summary>
    internal async Task<IReadOnlyList<DataMigrationState>> CreateManyAsync(IReadOnlyList<DataMigrationState> partitions,
        CancellationToken cancellationToken)
    {
        var now = Now;
        foreach (var partition in partitions)
        {
            partition.Id = CombGuid.Generate();
            partition.CreationDate = partition.RevisionDate = now;
        }

        try
        {
            await UseAsync(db =>
            {
                db.AddRange(partitions);
                return db.SaveChangesAsync(cancellationToken);
            });
            return partitions;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var existing = await GetManyAsync(partitions[0].Name, cancellationToken);
            if (existing.Count == 0)
            {
                throw;
            }

            return existing;
        }
    }

    /// <summary>
    /// Leases an incomplete partition that is unleased, expired, or already ours, and returns its current state.
    /// </summary>
    internal Task<DataMigrationState?> AcquireLeaseAsync(Guid id, string owner, TimeSpan duration, bool respectPause,
        CancellationToken cancellationToken)
    {
        var now = Now;
        return UseAsync(async db =>
        {
            var states = db.Set<DataMigrationState>();
            var acquired = await states
                .Where(s => s.Id == id && s.CompletedDate == null &&
                            (s.LeaseOwner == null || s.LeaseOwner == owner || s.LeaseExpiresDate < now) &&
                            (!respectPause || s.PausedDate == null))
                .ExecuteUpdateAsync(u => u
                    .SetProperty(s => s.LeaseOwner, owner)
                    .SetProperty(s => s.LeaseExpiresDate, now + duration)
                    .SetProperty(s => s.RevisionDate, now), cancellationToken);
            return acquired == 1 ? await states.AsNoTracking().SingleAsync(s => s.Id == id, cancellationToken) : null;
        });
    }

    /// <summary>
    /// Records a batch and renews the lease, only while <paramref name="owner"/> still holds it.
    /// </summary>
    internal Task<bool> CheckpointAsync(Guid id, string owner, DataMigrationCheckpoint checkpoint, TimeSpan leaseDuration,
        bool respectPause, CancellationToken cancellationToken)
    {
        var now = Now;
        DateTime? completed = checkpoint.Completed ? now : null;
        return UseAsync(async db => await db.Set<DataMigrationState>()
            .Where(s => s.Id == id && s.LeaseOwner == owner && (!respectPause || s.PausedDate == null))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Cursor, checkpoint.Cursor)
                .SetProperty(s => s.RowsScanned, s => s.RowsScanned + checkpoint.Scanned)
                .SetProperty(s => s.RowsConverted, s => s.RowsConverted + checkpoint.Converted)
                .SetProperty(s => s.RowsSkippedByRace, s => s.RowsSkippedByRace + checkpoint.Skipped)
                .SetProperty(s => s.RowsFailed, s => s.RowsFailed + checkpoint.Failed)
                .SetProperty(s => s.LeaseExpiresDate, now + leaseDuration)
                .SetProperty(s => s.StartedDate, s => s.StartedDate ?? now)
                .SetProperty(s => s.CompletedDate, completed)
                .SetProperty(s => s.RevisionDate, now), cancellationToken) == 1);
    }

    internal Task ReleaseLeaseAsync(Guid id, string owner, CancellationToken cancellationToken)
    {
        var now = Now;
        return UseAsync(db => db.Set<DataMigrationState>()
            .Where(s => s.Id == id && s.LeaseOwner == owner)
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.LeaseOwner, (string?)null)
                .SetProperty(s => s.LeaseExpiresDate, (DateTime?)null)
                .SetProperty(s => s.RevisionDate, now), cancellationToken));
    }

    private async Task<T> UseAsync<T>(Func<DbContext, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<DbContext>());
    }
}
