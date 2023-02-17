#nullable enable
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories;

public interface IAccessPolicyRepository
{
    Task<List<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> baseAccessPolicies);
    Task<bool> AccessPolicyExists(BaseAccessPolicy baseAccessPolicy);
    Task<BaseAccessPolicy?> GetByIdAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByGrantedProjectIdAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByGrantedServiceAccountIdAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByServiceAccountIdAsync(Guid id, Guid userId,
        AccessClientType accessType);
    Task ReplaceAsync(BaseAccessPolicy baseAccessPolicy);
    Task DeleteAsync(Guid id);
}
