#nullable enable
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class UpdateProjectServiceAccountsAccessPoliciesCommand : IUpdateProjectServiceAccountsAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public UpdateProjectServiceAccountsAccessPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task UpdateAsync(ProjectServiceAccountsAccessPoliciesUpdates accessPoliciesUpdates)
    {
        if (!accessPoliciesUpdates.ServiceAccountAccessPolicyUpdates.Any())
        {
            return;
        }

        await _accessPolicyRepository.UpdateProjectServiceAccountsAccessPoliciesAsync(accessPoliciesUpdates);
    }
}
