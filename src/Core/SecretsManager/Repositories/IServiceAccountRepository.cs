using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface IServiceAccountRepository
{
    Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<ServiceAccount> GetByIdAsync(Guid id);
    Task<IEnumerable<ServiceAccount>> GetManyByIds(IEnumerable<Guid> ids);
    Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount);
    Task ReplaceAsync(ServiceAccount serviceAccount);
    Task DeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<(bool Read, bool Write)> AccessToServiceAccountAsync(Guid id, Guid userId, AccessClientType accessType);
    Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToServiceAccountsAsync(IEnumerable<Guid> ids, Guid userId,
        AccessClientType accessType);
    Task<int> GetServiceAccountCountByOrganizationIdAsync(Guid organizationId);
    Task<int> GetServiceAccountCountByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<ServiceAccountCounts> GetServiceAccountCountsByIdAsync(Guid serviceAccountId, Guid userId, AccessClientType accessType);

    Task<IEnumerable<ServiceAccountSecretsDetails>> GetManyByOrganizationIdWithSecretsDetailsAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<bool> ServiceAccountsAreInOrganizationAsync(List<Guid> serviceAccountIds, Guid organizationId);
}
