using Bit.Core.Repositories;
using Bit.Pam.Entities;

namespace Bit.Pam.Repositories;

public interface IPamTargetSystemRepository : IRepository<PamTargetSystem, Guid>
{
    Task<ICollection<PamTargetSystem>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Deletes the target's access connector assignments, then the target itself, in one transaction. Re-checks
    /// under lock that no rotation config still names the target and returns false without deleting when one
    /// appeared after the caller's own check, so the delete can never orphan a config's credential.
    /// </summary>
    Task<bool> DeleteWithAssignmentsAsync(Guid targetSystemId);
}
