using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.DataMigrations;

/// <param name="Owner">Identifies the run's leases; a run takes back leases its owner already holds.</param>
/// <param name="Scheduled">Skip paused partitions, and stop one that's paused mid-run. Other runs ignore pauses.</param>
/// <param name="MaxBatches">The most batches to run per partition.</param>
/// <param name="DryRun">Read and transform from each partition's cursor without leasing or writing anything.</param>
public record DataMigrationRunOptions(string Owner, bool Scheduled = false, int? MaxBatches = null, bool DryRun = false);

public enum PartitionRunStatus
{
    Completed,
    BatchLimit,
    Stopped,
    Error,
}

/// <param name="FailedKeys">The keys of the first 100 failed rows.</param>
public record PartitionRunResult(int Partition, PartitionRunStatus Status, long Scanned, long Converted, long Skipped,
    long Failed, IReadOnlyList<string> FailedKeys, IReadOnlyList<TimeSpan> BatchDurations);

public interface IDataMigrationRunner
{
    IEnumerable<string> Names { get; }

    /// <summary>Splits a migration into paused partitions, unless it already has them.</summary>
    Task EnsurePartitionsAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards a migration's partitions and progress, and splits it again into paused partitions from the table as it
    /// is now, so rows written since the last split are covered.
    /// </summary>
    Task ResetAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Runs a migration's incomplete partitions.</summary>
    Task<IReadOnlyList<PartitionRunResult>> RunAsync(string name, DataMigrationRunOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a migration until every partition completes, ignoring pauses and first resetting partitions that completed
    /// with failed rows. Returns whether it completed without failed rows.
    /// </summary>
    Task<bool> RunToCompletionAsync(string name, CancellationToken cancellationToken = default);
}

internal class DataMigrationRunner(IEnumerable<DataMigration> migrations, IServiceScopeFactory scopeFactory,
    DataMigrationStateRepository repository, DataMigrationMetrics metrics, TimeProvider timeProvider,
    ILogger<DataMigrationRunner> logger) : IDataMigrationRunner
{
    private readonly Dictionary<string, DataMigration> _migrations = migrations.ToDictionary(m => m.Name);

    internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
    internal TimeSpan ErrorDelay { get; set; } = TimeSpan.FromSeconds(20);

    public IEnumerable<string> Names => _migrations.Keys;

    public async Task EnsurePartitionsAsync(string name, CancellationToken cancellationToken = default)
    {
        if ((await repository.GetManyAsync(name, cancellationToken)).Count > 0)
        {
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var partitions = await _migrations[name].SplitAsync(scope.ServiceProvider.GetRequiredService<DbContext>(), cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        partitions.ForEach(p => p.PausedDate = now);
        await repository.CreateManyAsync(partitions, cancellationToken);
    }

    public async Task ResetAsync(string name, CancellationToken cancellationToken = default)
    {
        await repository.DeleteManyAsync(name, cancellationToken);
        await EnsurePartitionsAsync(name, cancellationToken);
    }

    public async Task<IReadOnlyList<PartitionRunResult>> RunAsync(string name, DataMigrationRunOptions options,
        CancellationToken cancellationToken = default)
    {
        var migration = _migrations[name];
        var partitions = (await repository.GetManyAsync(name, cancellationToken)).Where(p => p.CompletedDate == null);
        var results = new ConcurrentBag<PartitionRunResult>();

        await Parallel.ForEachAsync(partitions,
            new ParallelOptions { MaxDegreeOfParallelism = migration.MaxParallelBatches, CancellationToken = cancellationToken },
            async (partition, ct) => results.Add(await RunPartitionAsync(migration, partition, options, ct)));

        return results.OrderBy(r => r.Partition).ToList();
    }

    public async Task<bool> RunToCompletionAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsurePartitionsAsync(name, cancellationToken);
        await repository.ResetFailedAsync(name, cancellationToken);

        for (var errors = 0; ;)
        {
            var results = await RunAsync(name, new(Environment.MachineName), cancellationToken);
            var states = await repository.GetManyAsync(name, cancellationToken);
            if (states.All(s => s.CompletedDate != null))
            {
                return states.Sum(s => s.RowsFailed) == 0;
            }

            var error = results.Any(r => r.Status == PartitionRunStatus.Error);
            if (error && ++errors == 3)
            {
                return false;
            }

            await Task.Delay(error ? ErrorDelay : RetryDelay, timeProvider, cancellationToken);
        }
    }

    private async Task<PartitionRunResult> RunPartitionAsync(DataMigration migration, DataMigrationState partition,
        DataMigrationRunOptions options, CancellationToken cancellationToken)
    {
        long scanned = 0, converted = 0, skipped = 0, failed = 0;
        var failedKeys = new List<string>();
        var durations = new List<TimeSpan>();
        PartitionRunResult Result(PartitionRunStatus status) =>
            new(partition.Partition, status, scanned, converted, skipped, failed, failedKeys, durations);

        var state = options.DryRun
            ? partition
            : await repository.AcquireLeaseAsync(partition.Id, options.Owner, migration.LeaseDuration,
                respectPause: options.Scheduled, cancellationToken);

        if (state is null)
        {
            logger.LogInformation(
                "Data migration {Name} partition {Partition} didn't start: paused {PausedDate}, leased by {LeaseOwner} until {LeaseExpiresDate}",
                migration.Name, partition.Partition, partition.PausedDate, partition.LeaseOwner, partition.LeaseExpiresDate);
            return Result(PartitionRunStatus.Stopped);
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<DbContext>();
            var cursor = state.Cursor;

            for (var batches = 0; batches < (options.MaxBatches ?? int.MaxValue); batches++)
            {
                var start = timeProvider.GetTimestamp();
                var batch = await migration.RunBatchAsync(db, cursor, state.RangeEnd, options.DryRun, logger, cancellationToken);

                cursor = batch.Cursor;
                scanned += batch.Scanned;
                converted += batch.Converted;
                skipped += batch.Skipped;
                failed += batch.Failures.Count;

                foreach (var failure in batch.Failures)
                {
                    logger.LogWarning("Data migration {Name} failed to convert row {Key}: {Error}", migration.Name,
                        failure.Key, failure.Error);
                    if (failedKeys.Count < 100)
                    {
                        failedKeys.Add(failure.Key);
                    }
                }

                var checkpoint = new DataMigrationCheckpoint(cursor, batch.Scanned, batch.Converted, batch.Skipped,
                    batch.Failures.Count, batch.Done);

                if (!options.DryRun && !await repository.CheckpointAsync(state.Id, options.Owner, checkpoint,
                        migration.LeaseDuration, respectPause: options.Scheduled, cancellationToken))
                {
                    logger.LogInformation(
                        "Data migration {Name} partition {Partition} stopped at batch {Batch}: paused, reset or leased by another owner",
                        migration.Name, partition.Partition, batches + 1);

                    return Result(PartitionRunStatus.Stopped);
                }

                durations.Add(timeProvider.GetElapsedTime(start));
                if (!options.DryRun)
                {
                    metrics.RecordBatch(migration.Name, partition.Partition, batch,
                        state.TotalRows - state.RowsScanned - scanned, durations[^1]);
                }

                if (batch.Done)
                {
                    return Result(PartitionRunStatus.Completed);
                }
            }

            return Result(PartitionRunStatus.BatchLimit);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Data migration {Name} partition {Partition} failed", migration.Name, partition.Partition);
            return Result(PartitionRunStatus.Error);
        }
        finally
        {
            if (!options.DryRun)
            {
                await repository.ReleaseLeaseAsync(state.Id, options.Owner, CancellationToken.None);
            }
        }
    }
}
