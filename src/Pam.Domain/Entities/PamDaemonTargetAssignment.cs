using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Pam.Entities;

/// <summary>
/// Grants a <see cref="PamDaemon"/> the ability to claim rotation jobs against a <see cref="PamTargetSystem"/>.
/// Invariant <c>OneAssignmentPerDaemonTarget</c> — at most one assignment may exist for a given daemon/target pair.
/// </summary>
public class PamDaemonTargetAssignment : ITableObject<Guid>
{
    public Guid Id { get; set; }

    public Guid DaemonId { get; set; }
    public Guid TargetSystemId { get; set; }
    public Guid OrganizationId { get; set; }

    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        Id = CombGuid.Generate();
    }
}
