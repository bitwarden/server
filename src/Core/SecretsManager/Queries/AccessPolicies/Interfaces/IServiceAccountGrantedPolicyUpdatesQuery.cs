#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data;

namespace Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;

public interface IServiceAccountGrantedPolicyUpdatesQuery
{
    Task<ServiceAccountGrantedPoliciesUpdates> GetAsync(ServiceAccount serviceAccount, ServiceAccountGrantedPolicies grantedPolicies);
}
