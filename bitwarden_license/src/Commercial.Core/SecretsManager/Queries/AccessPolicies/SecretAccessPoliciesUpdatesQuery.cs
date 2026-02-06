#nullable enable
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;

public class SecretAccessPoliciesUpdatesQuery : ISecretAccessPoliciesUpdatesQuery
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public SecretAccessPoliciesUpdatesQuery(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task<SecretAccessPoliciesUpdates> GetAsync(SecretAccessPolicies accessPolicies, Guid userId)
    {
        var currentPolicies = await _accessPolicyRepository.GetSecretAccessPoliciesAsync(accessPolicies.SecretId, userId);

        return currentPolicies == null ? new SecretAccessPoliciesUpdates(accessPolicies) : currentPolicies.GetPolicyUpdates(accessPolicies);
    }
}
