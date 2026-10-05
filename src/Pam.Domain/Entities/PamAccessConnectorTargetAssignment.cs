using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>
/// Grants a <see cref="PamAccessConnector"/> the ability to claim rotation jobs against a
/// <see cref="PamTargetSystem"/>. Invariant <c>OneAssignmentPerConnectorTarget</c> — at most one assignment may exist
/// for a given access connector/target pair.
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
