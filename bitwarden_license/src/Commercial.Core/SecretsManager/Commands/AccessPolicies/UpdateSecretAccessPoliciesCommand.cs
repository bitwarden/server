#nullable enable
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class UpdateSecretAccessPoliciesCommand : IUpdateSecretAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public UpdateSecretAccessPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task UpdateAsync(SecretAccessPoliciesUpdates accessPoliciesUpdates)
    {
        if (!accessPoliciesUpdates.HasUpdates())
        {
            return;
        }

        await _accessPolicyRepository.UpdateSecretAccessPoliciesAsync(accessPoliciesUpdates);
    }
}
