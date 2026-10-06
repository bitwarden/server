namespace Bit.DataMigrations;

/// <summary>
/// One partition of a data migration. Frozen: data migrations run against this table at old schema positions, so
/// its columns and what they mean never change.
/// </summary>
public class DataMigrationState
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public int Partition { get; set; }
    public string RangeStart { get; set; } = null!;
    public string RangeEnd { get; set; } = null!;
    public long TotalRows { get; set; }
    public string Cursor { get; set; } = null!;
    public long RowsScanned { get; set; }
    public long RowsConverted { get; set; }
    public long RowsSkippedByRace { get; set; }
    public long RowsFailed { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresDate { get; set; }
    public DateTime? PausedDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
}
