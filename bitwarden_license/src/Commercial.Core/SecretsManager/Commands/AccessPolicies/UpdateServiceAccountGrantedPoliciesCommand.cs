#nullable enable
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class UpdateServiceAccountGrantedPoliciesCommand : IUpdateServiceAccountGrantedPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public UpdateServiceAccountGrantedPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task UpdateAsync(ServiceAccountGrantedPoliciesUpdates grantedPoliciesUpdates)
    {
        if (!grantedPoliciesUpdates.ProjectGrantedPolicyUpdates.Any())
        {
            return;
        }

        await _accessPolicyRepository.UpdateServiceAccountGrantedPoliciesAsync(grantedPoliciesUpdates);
    }
}
