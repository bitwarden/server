using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationIntegrationConfiguration : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationIntegrationId { get; set; }
    public EventType EventType { get; set; }
    public string? Configuration { get; set; }
    public string? Template { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public string? Filters { get; set; }
    public void SetNewId() => Id = CoreHelpers.GenerateComb();
}
