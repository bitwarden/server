using Bit.Core.Dirt.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.Dirt.Entities;

public class OrganizationDeleteTask : ITableObject<Guid>
{
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
    public void SetNewId() => Id = CoreHelpers.GenerateComb();
}
