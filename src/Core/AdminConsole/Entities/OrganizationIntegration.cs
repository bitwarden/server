using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationIntegration : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public IntegrationType Type { get; set; }
    public string? Configuration { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public void SetNewId() => Id = CoreHelpers.GenerateComb();
}
