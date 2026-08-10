#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// Durable state and single-flight coordination for <see cref="BaseDataMigration{TRow, TUpdate}"/>
/// implementations. One row per (migration, partition) in the DataMigrationState table.
/// </summary>
public interface IDataMigrationStateRepository
{
    /// <summary>True when partition rows exist for this migration.</summary>
    Task<bool> ExistsAsync(string name, CancellationToken token);

    /// <summary>
    /// Inserts all partition rows in one atomic operation. The composite primary key
    /// (Name, Partition) is the initialization mutex: when two instances race, the loser's insert
    /// fails whole on a duplicate key, is swallowed, and the winner's boundaries stand.
    /// </summary>
    Task InitializeAsync(string name, IReadOnlyList<PartitionRange> partitions, CancellationToken token);

    /// <summary>
    /// Atomically leases any available partition — incomplete, and unleased or lease-expired.
    /// Returns null when nothing is available. Never blocks: concurrent claimers skip one another
    /// rather than queue.
    /// </summary>
    Task<PartitionClaim?> TryClaimAsync(string name, string owner, TimeSpan leaseDuration,
        CancellationToken token);

    /// <summary>
    /// Persists a partition's checkpoint, fenced by ownership, and renews the lease for another
    /// <paramref name="leaseDuration"/> — a processor holds its partition for as long as it keeps
    /// checkpointing. False means the lease was lost (this owner no longer holds the partition)
    /// and the caller must yield without retrying.
    /// </summary>
    Task<bool> CheckpointAsync(string name, int partition, string owner, MigrationCheckpoint checkpoint,
        TimeSpan leaseDuration, CancellationToken token);

    /// <summary>Releases the lease if still held by this owner; lease expiry is the backstop.</summary>
    Task ReleaseAsync(string name, int partition, string owner, CancellationToken token);

    /// <summary>Every partition's progress, read regardless of leases — the source for the
    /// pending-rows gauge tallied each firing.</summary>
    Task<IReadOnlyList<PartitionProgress>> ReadProgressAsync(string name, CancellationToken token);

    /// <summary>Partitions not yet completed for this migration.</summary>
    Task<int> ReadIncompleteCountAsync(string name, CancellationToken token);
}
