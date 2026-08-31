using Bit.Core.Repositories;
using Bit.Pam.Entities;

namespace Bit.Pam.Repositories;

public interface IPamTargetSystemRepository : IRepository<PamTargetSystem, Guid>
{
    Task<ICollection<PamTargetSystem>> GetManyByOrganizationIdAsync(Guid organizationId);
}
