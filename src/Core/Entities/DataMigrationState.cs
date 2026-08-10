#nullable enable

using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

namespace Bit.Core.Entities;

/// <summary>
/// One partition of a gradual data migration's durable state. Comb-keyed like every other entity,
/// with a unique index over (Name, Partition) — the natural key that makes duplicate partition
/// rows impossible and serves as the initialization mutex. Single-flight coordination happens
/// through conditional updates on the lease columns; progress is the opaque <see cref="Cursor"/>
/// owned by the migration.
/// </summary>
public class DataMigrationState : ITableObject<Guid>
{
    public Guid Id { get; set; }
    [MaxLength(100)]
    public string Name { get; set; } = null!;
    public int Partition { get; set; }
    [MaxLength(300)]
    public string? RangeStart { get; set; }
    [MaxLength(300)]
    public string? RangeEnd { get; set; }
    [MaxLength(300)]
    public string? Cursor { get; set; }
    public long TotalRows { get; set; }
    public long RowsScanned { get; set; }
    public long RowsConverted { get; set; }
    public long RowsSkippedByRace { get; set; }
    public long RowsFailed { get; set; }
    [MaxLength(100)]
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresDate { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
