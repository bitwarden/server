using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories.Noop;

public class NoopServiceAccountRepository : IServiceAccountRepository
{
    public Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType)
    {
        return Task.FromResult(null as IEnumerable<ServiceAccount>);
    }

    public Task<ServiceAccount> GetByIdAsync(Guid id)
    {
        return Task.FromResult(null as ServiceAccount);
    }

    public Task<IEnumerable<ServiceAccount>> GetManyByIds(IEnumerable<Guid> ids)
    {
        return Task.FromResult(null as IEnumerable<ServiceAccount>);
    }

    public Task<ServiceAccount> CreateAsync(ServiceAccount serviceAccount)
    {
        return Task.FromResult(null as ServiceAccount);
    }

    public Task ReplaceAsync(ServiceAccount serviceAccount)
    {
        return Task.FromResult(0);
    }

    public Task DeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task<bool> UserHasReadAccessToServiceAccount(Guid id, Guid userId)
    {
        return Task.FromResult(false);
    }

    public Task<bool> UserHasWriteAccessToServiceAccount(Guid id, Guid userId)
    {
        return Task.FromResult(false);
    }

    public Task<IEnumerable<ServiceAccount>> GetManyByOrganizationIdWriteAccessAsync(Guid organizationId, Guid userId, AccessClientType accessType) => throw new NotImplementedException();

    public Task<(bool Read, bool Write)> AccessToServiceAccountAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        return Task.FromResult((false, false));
    }

    public Task<int> GetServiceAccountCountByOrganizationIdAsync(Guid organizationId)
    {
        return Task.FromResult(0);
    }
}
