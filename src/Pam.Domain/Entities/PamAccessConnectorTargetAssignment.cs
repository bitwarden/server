using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>
/// Lets a <see cref="PamAccessConnector"/> claim rotation jobs on a <see cref="PamTargetSystem"/>. At most one exists
/// per pair (<c>OneAssignmentPerConnectorTarget</c>).
/// </summary>
public class PamAccessConnectorTargetAssignment : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid AccessConnectorId { get; set; }
    public Guid TargetSystemId { get; set; }
    public Guid OrganizationId { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
