using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories.Noop;

public class NoopSecretRepository : ISecretRepository
{
    public Task<IEnumerable<SecretPermissionDetails>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId,
        AccessClientType accessType)
    {
        return Task.FromResult(null as IEnumerable<SecretPermissionDetails>);
    }

    public Task<IEnumerable<SecretPermissionDetails>> GetManyByOrganizationIdInTrashAsync(Guid organizationId)
    {
        return Task.FromResult(null as IEnumerable<SecretPermissionDetails>);
    }

    public Task<IEnumerable<Secret>> GetManyByOrganizationIdInTrashByIdsAsync(Guid organizationId,
        IEnumerable<Guid> ids)
    {
        return Task.FromResult(null as IEnumerable<Secret>);
    }

    public Task<IEnumerable<Secret>> GetManyByIds(IEnumerable<Guid> ids)
    {
        return Task.FromResult(null as IEnumerable<Secret>);
    }

    public Task<IEnumerable<SecretPermissionDetails>> GetManyByProjectIdAsync(Guid projectId, Guid userId,
        AccessClientType accessType)
    {
        return Task.FromResult(null as IEnumerable<SecretPermissionDetails>);
    }

    public Task<Secret> GetByIdAsync(Guid id)
    {
        return Task.FromResult(null as Secret);
    }

    public Task<Secret> CreateAsync(Secret secret)
    {
        return Task.FromResult(null as Secret);
    }

    public Task<Secret> UpdateAsync(Secret secret)
    {
        return Task.FromResult(null as Secret);
    }

    public Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task HardDeleteManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task RestoreManyByIdAsync(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task<IEnumerable<Secret>> ImportAsync(IEnumerable<Secret> secrets)
    {
        return Task.FromResult(null as IEnumerable<Secret>);
    }

    public Task UpdateRevisionDates(IEnumerable<Guid> ids)
    {
        return Task.FromResult(0);
    }

    public Task<(bool Read, bool Write)> AccessToSecretAsync(Guid id, Guid userId, AccessClientType accessType)
    {
        return Task.FromResult((false, false));
    }

    public Task EmptyTrash(DateTime nowTime, uint deleteAfterThisNumberOfDays)
    {
        return Task.FromResult(0);
    }

    public Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId)
    {
        return Task.FromResult(0);
    }
}
