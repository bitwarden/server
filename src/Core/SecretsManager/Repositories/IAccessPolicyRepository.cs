#nullable enable
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Repositories;

public interface IAccessPolicyRepository
{
    Task<List<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> baseAccessPolicies);
    Task<bool> AccessPolicyExists(BaseAccessPolicy baseAccessPolicy);
    Task<BaseAccessPolicy?> GetByIdAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>?> GetManyByProjectId(Guid id);
    Task ReplaceAsync(BaseAccessPolicy baseAccessPolicy);
    Task DeleteAsync(Guid id);
}
