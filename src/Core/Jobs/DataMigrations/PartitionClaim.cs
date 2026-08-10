#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// A successfully leased partition, carrying the state needed to resume it.
/// </summary>
public record PartitionClaim(
    int Partition,
    string? RangeStart,
    string? RangeEnd,
    string? Cursor,
    long TotalRows,
    long RowsScanned,
    long RowsConverted,
    long RowsSkippedByRace,
    long RowsFailed,
    DateTime? StartedDate)
{
    /// <summary>
    /// Builds the checkpoint for this partition by applying one batch's deltas to the running
    /// totals.
    /// </summary>
    public MigrationCheckpoint ToCheckpoint(
        string? cursor,
        int scannedDelta = 0,
        int convertedDelta = 0,
        int racedDelta = 0,
        int failedDelta = 0,
        DateTime? startedDate = null,
        DateTime? completedDate = null) =>
        new(cursor,
            RowsScanned + scannedDelta,
            RowsConverted + convertedDelta,
            RowsSkippedByRace + racedDelta,
            RowsFailed + failedDelta,
            startedDate ?? StartedDate,
            completedDate);
}
