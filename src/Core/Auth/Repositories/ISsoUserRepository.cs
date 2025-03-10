using Bit.Core.Auth.Entities;
using Bit.Core.Repositories;

#nullable enable

namespace Bit.Core.Auth.Repositories;

public interface ISsoUserRepository : IRepository<SsoUser, long>
{
    Task DeleteAsync(Guid userId, Guid? organizationId);
    Task<SsoUser?> GetByUserIdOrganizationIdAsync(Guid organizationId, Guid userId);
}
