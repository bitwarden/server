#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// One partition's progress as read straight from the state table — leased or not. Feeds the
/// datamigration.pending_rows gauge: pending = TotalRows − RowsScanned (0 once completed).
/// </summary>
public record PartitionProgress(int Partition, long TotalRows, long RowsScanned, bool Completed);
