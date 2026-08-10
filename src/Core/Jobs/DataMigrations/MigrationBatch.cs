#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// The result of one windowed read against a migration's source table.
/// </summary>
/// <param name="Rows">Candidate rows only — rows the migration may need to transform. Never more
/// than the migration's BatchSize.</param>
/// <param name="NextCursor">The scan high-water mark: the key of the last row examined (or, when a
/// dense window over-delivers, the last row taken). This is what gets checkpointed — the cursor
/// tracks scanning progress, not conversion progress. Null only when nothing was scanned.</param>
/// <param name="ScannedCount">Rows examined to produce this batch, including non-candidates.</param>
/// <param name="EndOfRange">True when the scan reached the end of the partition's range. This is
/// the only completion signal — an empty <see cref="Rows"/> alone just means the window held no
/// candidates.</param>
public record MigrationBatch<TRow>(
    IReadOnlyList<TRow> Rows,
    string? NextCursor,
    int ScannedCount,
    bool EndOfRange);
