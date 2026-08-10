#nullable enable

using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// Template for gradual, resumable data migrations over large tables. Each
/// <see cref="RunAsync"/> (one firing of <see cref="DataMigrationsJob"/>) claims up to
/// <see cref="MaxParallelBatches"/> partitions and drains each one it holds: windowed read →
/// shape → conditional write → fenced checkpoint, repeated batch after batch until the range
/// completes, with every checkpoint renewing the lease. The firing cadence bounds how quickly
/// work starts and how quickly a lost partition resumes — not throughput.
///
/// <para>Implementers define only how to read and shape rows. Everything else — partition state,
/// leasing, checkpoint ordering, completion detection, failure isolation, telemetry — is
/// engine-owned.</para>
///
/// <para>Rules the engine cannot enforce mechanically, required of every implementation:</para>
/// <list type="number">
/// <item><see cref="ReadBatchAsync"/> reads RAW stored values — bypassing repository unprotect
/// paths and EF value converters — with cursor comparison and ordering evaluated by the database,
/// bounded by <see cref="ScanWindow"/> rows per statement, never straying outside
/// (cursor, rangeEnd].</item>
/// <item><see cref="WriteBatchAsync"/> is conditional per row (compare-and-swap on the value that
/// was read; never a blind UPDATE by key), runs in one transaction, and has committed before it
/// returns. Keep BatchSize × (1 + secondary indexes touched by the update) well under SQL Server's
/// ~5,000-lock escalation threshold.</item>
/// <item>The whole batch is idempotent: replays after a crash must be no-ops.</item>
/// <item>Do not enable a migration until every writer already writes the target format — rows
/// written mid-migration can sort behind the cursor on any provider.</item>
/// </list>
/// </summary>
public abstract class BaseDataMigration<TRow, TUpdate> : IDataMigration
    where TUpdate : class
{
    private static readonly Meter _meter = new("Bit.Core.DataMigrations");
    private static readonly Counter<long> _rowCounter = _meter.CreateCounter<long>("datamigration.rows");
    private static readonly Gauge<long> _pendingGauge = _meter.CreateGauge<long>("datamigration.pending_rows");

    // Partitions currently being drained in this process, per migration name. Job firings may
    // overlap, so the MaxParallelBatches cap is enforced here, across firings, not per firing.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _activeDrains = new();

    private readonly IDataMigrationStateRepository _stateRepository;
    private readonly TimeProvider _timeProvider;
    protected readonly ILogger _logger;

    protected BaseDataMigration(
        IDataMigrationStateRepository stateRepository,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _stateRepository = stateRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <summary>Per-migration gate (feature flag, cloud and self-hosted alike). Also the terminal latch:
    /// turn it off once the migration completes and is verified.</summary>
    protected abstract bool Enabled { get; }

    /// <summary>Maximum candidate rows per write statement. See rule 2 for the ceiling.</summary>
    protected virtual int BatchSize => 1000;

    /// <summary>Rows examined per read statement. Bounds statement duration independently of
    /// candidate density; size at roughly BatchSize ÷ expected candidate density.</summary>
    protected virtual int ScanWindow => BatchSize;

    /// <summary>Ranges the keyspace is split into. Fixed at first run; 1 = sequential.</summary>
    protected virtual int PartitionCount => 1;

    /// <summary>Partitions this process drains concurrently, enforced across overlapping firings.
    /// Effective global parallelism ≤ min(PartitionCount, instances × this).</summary>
    protected virtual int MaxParallelBatches => 1;

    /// <summary>Must exceed the worst-case duration of one batch — not the whole partition. Each
    /// fenced checkpoint renews the lease, so a healthy processor holds its partition
    /// indefinitely, while a crashed or stalled one stops renewing and its partition is claimable
    /// again within one duration, resuming from the last checkpoint.</summary>
    protected virtual TimeSpan LeaseDuration => TimeSpan.FromMinutes(10);

    /// <summary>Total rows in the target table, read once at first run. Anchors each partition's
    /// TotalRows — the baseline the pending-rows metric subtracts scan progress from — and places
    /// the partition boundaries. An estimate is fine; accuracy only affects how evenly ranges
    /// split and how exact the pending gauge reads.</summary>
    protected abstract Task<long> CountRowsAsync(CancellationToken token);

    /// <summary>The cursor value of the row at the zero-based <paramref name="offset"/> of the
    /// target table, in the same database-side key order <see cref="ReadBatchAsync"/> paginates
    /// by (OFFSET @offset ROWS FETCH NEXT 1 ROW ONLY / ORDER BY + Skip). Null when the offset
    /// falls past the end of the table. Required when <see cref="PartitionCount"/> &gt; 1 (unless
    /// <see cref="GetPartitionsAsync"/> is overridden wholesale).</summary>
    protected virtual Task<string?> ReadKeyAtOffsetAsync(long offset, CancellationToken token) =>
        throw new NotSupportedException(
            $"{GetType().Name} must override ReadKeyAtOffsetAsync (or GetPartitionsAsync) " +
            "when PartitionCount > 1.");

    /// <summary>The N ranges the keyspace splits into, each with its sampled row count. The
    /// default divides the C counted rows into near-equal slices by sampling the key at each
    /// i·C/N offset — the one place OFFSET pagination is acceptable, because it runs once ever,
    /// at first initialization. Ranges then freeze into DataMigrationState; rows inserted later
    /// land in whichever range contains their key. Override wholesale for a custom split
    /// strategy.</summary>
    protected virtual async Task<IReadOnlyList<PartitionRange>> GetPartitionsAsync(CancellationToken token)
    {
        var count = await CountRowsAsync(token);
        if (PartitionCount <= 1)
        {
            return [new PartitionRange(0, null, null, count)];
        }

        var ranges = new List<PartitionRange>(PartitionCount);
        string? start = null;
        var lastOffset = -1L;
        for (var i = 1; i < PartitionCount; i++)
        {
            // Zero-based last row of the i-th slice, so each (prev, key] range holds ~C/N rows.
            // Tables smaller than N (or shrinking mid-sample) yield repeated or null samples;
            // skipping them merges adjacent slices, whose row estimates absorb the merge.
            var offset = count * i / PartitionCount - 1;
            if (offset <= lastOffset)
            {
                continue;
            }
            var key = await ReadKeyAtOffsetAsync(offset, token);
            if (key == null)
            {
                continue;
            }
            ranges.Add(new PartitionRange(ranges.Count, start, key, offset - lastOffset));
            start = key;
            lastOffset = offset;
        }
        ranges.Add(new PartitionRange(ranges.Count, start, null, count - 1 - lastOffset));
        return ranges;
    }

    /// <summary>One windowed read after <paramref name="cursor"/> (null = range start), clipped to
    /// <paramref name="rangeEnd"/> (null = range open). See rule 1.</summary>
    protected abstract Task<MigrationBatch<TRow>> ReadBatchAsync(
        string? cursor, string? rangeEnd, CancellationToken token);

    /// <summary>Transforms one candidate into its conditional update, or null when the row needs
    /// no work. Throwing marks the row failed; the engine logs, counts, and continues — one bad
    /// row never halts the migration.</summary>
    protected abstract TUpdate? Shape(TRow row);

    /// <summary>Applies updates per rules 2–3. Returns rows actually written; the shortfall is
    /// rows a concurrent writer already converted — skipped, never retried.</summary>
    protected abstract Task<int> WriteBatchAsync(IReadOnlyList<TUpdate> updates, CancellationToken token);

    /// <inheritdoc />
    public async Task RunAsync(CancellationToken token)
    {
        if (!Enabled)
        {
            return;
        }

        if (!await _stateRepository.ExistsAsync(Name, token))
        {
            await _stateRepository.InitializeAsync(Name, await GetPartitionsAsync(token), token);
        }

        // Tallied from the state table every firing — leased partitions included — so dashboards
        // see the migration-wide drain rate, not just this instance's share of it.
        foreach (var progress in await _stateRepository.ReadProgressAsync(Name, token))
        {
            RecordPending(progress.Partition,
                progress.Completed ? 0 : Math.Max(0, progress.TotalRows - progress.RowsScanned));
        }

        // Owner is unique per execution, not per process: firings may overlap, and if two drains
        // in one process shared a fence identity, a stalled drain whose lease expired could
        // checkpoint straight over its successor's cursor. Machine name kept for incident triage.
        var owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        // Disjoint partitions → disjoint write sets: parallel workers cannot deadlock one another
        // or double-convert a row. Each worker holds one drain slot; overlapping firings that find
        // no free slot (or no claimable partition) exit in milliseconds.
        await Task.WhenAll(Enumerable.Range(0, MaxParallelBatches).Select(async _ =>
        {
            if (!TryReserveDrainSlot())
            {
                return;
            }
            try
            {
                var claim = await _stateRepository.TryClaimAsync(Name, owner, LeaseDuration, token);
                if (claim != null)
                {
                    await ProcessPartitionAsync(claim, owner, token);
                }
            }
            finally
            {
                ReleaseDrainSlot();
            }
        }));
    }

    private bool TryReserveDrainSlot()
    {
        if (_activeDrains.AddOrUpdate(Name, 1, (_, active) => active + 1) <= MaxParallelBatches)
        {
            return true;
        }
        ReleaseDrainSlot();
        return false;
    }

    private void ReleaseDrainSlot() => _activeDrains.AddOrUpdate(Name, 0, (_, active) => active - 1);

    private async Task ProcessPartitionAsync(PartitionClaim claim, string owner, CancellationToken token)
    {
        try
        {
            // Drain the partition: every fenced checkpoint renews the lease, so a healthy
            // processor keeps its claim from first batch to end of range. Enabled is re-read
            // each iteration so the flag kills a run in progress, not just the next firing.
            while (Enabled && !token.IsCancellationRequested)
            {
                var batch = await ReadBatchAsync(claim.Cursor ?? claim.RangeStart, claim.RangeEnd, token);
                var now = _timeProvider.GetUtcNow().UtcDateTime;

                var updates = new List<TUpdate>(batch.Rows.Count);
                var failed = 0;
                foreach (var row in batch.Rows)
                {
                    try
                    {
                        if (Shape(row) is { } update)
                        {
                            updates.Add(update);
                        }
                    }
                    catch (Exception e)
                    {
                        failed++;
                        _logger.LogWarning(e,
                            "Migration {Name} partition {Partition}: failed to shape a row; skipping.",
                            Name, claim.Partition);
                    }
                }

                // The implementer commits inside WriteBatchAsync (rule 2)...
                var written = updates.Count == 0 ? 0 : await WriteBatchAsync(updates, token);

                // ...so the fenced checkpoint lands strictly after the data commit. A crash
                // between the two replays one window of idempotent no-ops; the reverse order
                // would lose rows.
                var checkpoint = claim.ToCheckpoint(
                    batch.NextCursor ?? claim.Cursor ?? claim.RangeStart,
                    scannedDelta: batch.ScannedCount,
                    convertedDelta: written,
                    racedDelta: updates.Count - written,
                    failedDelta: failed,
                    startedDate: claim.StartedDate ?? now,
                    completedDate: batch.EndOfRange ? now : null);
                var stillOwner = await _stateRepository.CheckpointAsync(Name, claim.Partition, owner,
                    checkpoint, LeaseDuration, token);
                if (!stillOwner)
                {
                    _logger.LogWarning(
                        "Migration {Name} partition {Partition}: lease lost mid-batch; yielding.",
                        Name, claim.Partition);
                    return;
                }

                RecordMetrics(batch.ScannedCount, written, updates.Count - written, failed);

                // Batch-resolution refresh for the partition being drained; the per-firing tally
                // covers every partition at trigger resolution.
                RecordPending(claim.Partition,
                    batch.EndOfRange ? 0 : Math.Max(0, claim.TotalRows - checkpoint.RowsScanned));

                if (batch.EndOfRange)
                {
                    if (await _stateRepository.ReadIncompleteCountAsync(Name, token) == 0)
                    {
                        _logger.LogInformation("Migration {Name} completed across all partitions.", Name);
                    }
                    return;
                }

                // Carry the just-persisted absolute state into the next batch.
                claim = claim with
                {
                    Cursor = checkpoint.Cursor,
                    RowsScanned = checkpoint.RowsScanned,
                    RowsConverted = checkpoint.RowsConverted,
                    RowsSkippedByRace = checkpoint.RowsSkippedByRace,
                    RowsFailed = checkpoint.RowsFailed,
                    StartedDate = checkpoint.StartedDate,
                };
            }
        }
        catch (Exception e)
        {
            // Exit the drain rather than retry hot: the next firing resumes from the last
            // checkpoint, so a persistent fault costs one firing interval per attempt instead of
            // a tight error loop. One partition's failure never stops its siblings either way.
            _logger.LogError(e, "Migration {Name} partition {Partition}: batch failed.",
                Name, claim.Partition);
        }
        finally
        {
            // Fenced by owner — a no-op when the lease was lost or already expired.
            await _stateRepository.ReleaseAsync(Name, claim.Partition, owner, CancellationToken.None);
        }
    }

    private void RecordPending(int partition, long pending) =>
        _pendingGauge.Record(pending,
            new KeyValuePair<string, object?>("migration", Name),
            new KeyValuePair<string, object?>("partition", partition));

    private void RecordMetrics(int scanned, int converted, int raced, int failed)
    {
        var tag = new KeyValuePair<string, object?>("migration", Name);
        _rowCounter.Add(scanned, tag, new KeyValuePair<string, object?>("outcome", "scanned"));
        _rowCounter.Add(converted, tag, new KeyValuePair<string, object?>("outcome", "converted"));
        _rowCounter.Add(raced, tag, new KeyValuePair<string, object?>("outcome", "raced"));
        _rowCounter.Add(failed, tag, new KeyValuePair<string, object?>("outcome", "failed"));
    }
}
