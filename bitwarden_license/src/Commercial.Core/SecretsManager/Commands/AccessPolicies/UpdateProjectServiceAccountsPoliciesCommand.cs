#nullable enable
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class UpdateProjectServiceAccountsPoliciesCommand : IUpdateProjectServiceAccountsPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public UpdateProjectServiceAccountsPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task UpdateAsync(ProjectServiceAccountsPoliciesUpdates policiesUpdates)
    {
        await _accessPolicyRepository.UpdateProjectServiceAccountsAccessPoliciesAsync(policiesUpdates);
    }
}
