using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories;

public interface IServiceAccountRepository
{
    Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId);
    Task<ServiceAccount> GetByIdAsync(Guid id);
    Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount);
    Task ReplaceAsync(ServiceAccount serviceAccount);
}
