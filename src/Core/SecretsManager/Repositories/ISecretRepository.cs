using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface ISecretRepository
{
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByOrganizationIdInTrashAsync(Guid organizationId);
    Task<IEnumerable<SecretPermissionDetails>> GetManyDetailsByProjectIdAsync(Guid projectId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByOrganizationIdAsync(Guid organizationId, Guid userId, AccessClientType accessType);
    Task<IEnumerable<Secret>> GetManyByOrganizationIdInTrashByIdsAsync(Guid organizationId, IEnumerable<Guid> ids);
    Task<IEnumerable<Secret>> GetManyByIds(IEnumerable<Guid> ids);
    Task<Secret> GetByIdAsync(Guid id);
    Task<Secret> CreateAsync(Secret secret);
    Task<Secret> UpdateAsync(Secret secret);
    Task SoftDeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task HardDeleteManyByIdAsync(IEnumerable<Guid> ids);
    Task RestoreManyByIdAsync(IEnumerable<Guid> ids);
    Task<IEnumerable<Secret>> ImportAsync(IEnumerable<Secret> secrets);
    Task<(bool Read, bool Write)> AccessToSecretAsync(Guid id, Guid userId, AccessClientType accessType);
    Task<Dictionary<Guid, (bool Read, bool Write)>> AccessToSecretsAsync(IEnumerable<Guid> secretIds, Guid userId, AccessClientType accessType);
    Task EmptyTrash(DateTime nowTime, uint deleteAfterThisNumberOfDays);
    Task<int> GetSecretsCountByOrganizationIdAsync(Guid organizationId);
}
