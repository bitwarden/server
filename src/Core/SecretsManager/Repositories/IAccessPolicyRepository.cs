#nullable enable
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Repositories;

public interface IAccessPolicyRepository
{
    Task<List<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> baseAccessPolicies);
    Task<bool> AccessPolicyExists(BaseAccessPolicy baseAccessPolicy);
    Task<BaseAccessPolicy?> GetByIdAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByGrantedProjectIdAsync(Guid id, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByGrantedServiceAccountIdAsync(Guid id, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> GetManyByServiceAccountIdAsync(Guid id, Guid userId,
        AccessClientType accessType);
    Task ReplaceAsync(BaseAccessPolicy baseAccessPolicy);
    Task DeleteAsync(Guid id);
    Task<IEnumerable<BaseAccessPolicy>> GetPeoplePoliciesByGrantedProjectIdAsync(Guid id, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> ReplaceProjectPeopleAsync(ProjectPeopleAccessPolicies peopleAccessPolicies, Guid userId);
    Task<PeopleGrantees> GetPeopleGranteesAsync(Guid organizationId, Guid currentUserId);
}
