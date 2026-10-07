using Bit.Core.Repositories;
using Bit.Pam.Entities;

namespace Bit.Pam.Repositories;

public interface IPamTargetSystemRepository : IRepository<PamTargetSystem, Guid>
{
    Task<ICollection<PamTargetSystem>> GetManyByOrganizationIdAsync(Guid organizationId);

    /// <summary>
    /// Deletes the target's assignments and then the target in one transaction. Re-checks under lock that no rotation
    /// config names it, so the delete cannot orphan a credential.
    /// </summary>
    Task<bool> DeleteWithAssignmentsAsync(Guid targetSystemId);
}
