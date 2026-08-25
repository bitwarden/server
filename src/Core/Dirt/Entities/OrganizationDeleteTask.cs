using Bit.Core.Dirt.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationDeleteTask : ITableObject<Guid>
{
    /// <summary>
    /// How long a claimed task stays leased before another worker may reclaim it. Every repository
    /// implementation has to agree on this, so it lives with the entity rather than in one of them.
    /// </summary>
    public const int LeaseDurationMinutes = 10;

    /// <summary>
    /// A task is abandoned once it has failed this many times, so a permanently failing cleanup
    /// cannot be reclaimed forever.
    /// </summary>
    public const int MaxFailureCount = 5;

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationDeleteTaskType TaskType { get; set; }
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public DateTime? StartDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public long ItemsDeletedCount { get; set; }
    public int FailureCount { get; set; }
    public string? LastError { get; set; }
    public void SetNewId() => Id = CombGuid.Generate();
}
