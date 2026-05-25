using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.PrivilegedAccessManagement.Repositories;

public interface ILeasingPolicyRepository : IRepository<LeasingPolicy, Guid>
{
    Task<ICollection<LeasingPolicy>> GetManyByOrganizationIdAsync(Guid organizationId);
}
