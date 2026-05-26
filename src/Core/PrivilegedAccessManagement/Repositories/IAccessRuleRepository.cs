using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.PrivilegedAccessManagement.Repositories;

public interface IAccessRuleRepository : IRepository<AccessRule, Guid>
{
    Task<ICollection<AccessRule>> GetManyByOrganizationIdAsync(Guid organizationId);
}
