using Bit.Core.Entities;
using Bit.Core.Enums;

namespace Bit.Core.Repositories;

public interface IServiceAccountRepository
{
    Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<ServiceAccount> GetByIdAsync(Guid id);
    Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount);
    Task ReplaceAsync(ServiceAccount serviceAccount);
    Task<bool> UserHasReadAccessToServiceAccount(Guid id, Guid userId);
    Task<bool> UserHasWriteAccessToServiceAccount(Guid id, Guid userId);
}
