#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// The absolute state persisted for a partition after one batch. Written only through the owner
/// fence (WHERE LeaseOwner = owner), strictly after the batch's data transaction has committed.
/// </summary>
public record MigrationCheckpoint(
    string? Cursor,
    long RowsScanned,
    long RowsConverted,
    long RowsSkippedByRace,
    long RowsFailed,
    DateTime? StartedDate,
    DateTime? CompletedDate);
