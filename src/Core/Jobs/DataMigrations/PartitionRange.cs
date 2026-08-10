#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// A contiguous slice of a migration's keyspace: (RangeStart, RangeEnd]. Boundaries are opaque
/// cursor values owned by the migration; null bounds are open (table start / table end).
/// <see cref="TotalRows"/> is the slice's row count as sampled at initialization — an estimate
/// that anchors the pending-rows metric, not an exact promise.
/// </summary>
public record PartitionRange(int Partition, string? RangeStart, string? RangeEnd, long TotalRows);
