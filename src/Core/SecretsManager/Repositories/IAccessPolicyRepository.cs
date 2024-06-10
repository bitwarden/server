#nullable enable
using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Repositories;

public interface IAccessPolicyRepository
{
    Task<List<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> baseAccessPolicies);
    Task<IEnumerable<BaseAccessPolicy>> GetPeoplePoliciesByGrantedProjectIdAsync(Guid id, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> ReplaceProjectPeopleAsync(ProjectPeopleAccessPolicies peopleAccessPolicies, Guid userId);
    Task<PeopleGrantees> GetPeopleGranteesAsync(Guid organizationId, Guid currentUserId);
    Task<IEnumerable<BaseAccessPolicy>> GetPeoplePoliciesByGrantedServiceAccountIdAsync(Guid id, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> ReplaceServiceAccountPeopleAsync(ServiceAccountPeopleAccessPolicies peopleAccessPolicies, Guid userId);
    Task<ServiceAccountGrantedPolicies?> GetServiceAccountGrantedPoliciesAsync(Guid serviceAccountId);
    Task<ServiceAccountGrantedPoliciesPermissionDetails?> GetServiceAccountGrantedPoliciesPermissionDetailsAsync(
        Guid serviceAccountId, Guid userId, AccessClientType accessClientType);
    Task UpdateServiceAccountGrantedPoliciesAsync(ServiceAccountGrantedPoliciesUpdates policyUpdates);
    Task<ProjectServiceAccountsAccessPolicies?> GetProjectServiceAccountsAccessPoliciesAsync(Guid projectId);
    Task UpdateProjectServiceAccountsAccessPoliciesAsync(ProjectServiceAccountsAccessPoliciesUpdates updates);
    Task<SecretAccessPolicies?> GetSecretAccessPoliciesAsync(Guid secretId, Guid userId);
}
