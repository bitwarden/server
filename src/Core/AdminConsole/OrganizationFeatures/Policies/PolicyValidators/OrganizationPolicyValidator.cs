using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository, IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories) : IPolicyValidator
{
    /* prototype comment:
     1. Individual policies that need to retrieve the bulk-affected users can implement this class.
     2. They can override FilterPolicyUsers to fit their needs.
     3. All child classes can integrate into the existing IPolicyValidator seamlessly.
    */
    public abstract PolicyType Type { get; }

    public abstract IEnumerable<PolicyType> RequiredPolicies { get; }


    public async Task<IEnumerable<T>> GetAsync<T>(PolicyUpdate policyUpdate) where T : IPolicyRequirement
    {
        var policyDetails = await policyRepository.PolicyDetailsReadByOrganizationIdAsync(policyUpdate.OrganizationId, policyUpdate.Type);
        var policyDetailGroups = policyDetails.GroupBy(policyDetail => policyDetail.OrganizationUserId);

        var requirements = new List<T>();

        foreach (var policyDetailGroup in policyDetailGroups)
        {
            requirements.Add(PolicyRequirementBuilder.Build<T>(factories, policyDetailGroup.ToList()));

        }

        return requirements;
    }

    public abstract Task OnSaveSideEffectsAsync(
        PolicyUpdate policyUpdate,
        Policy? currentPolicy
    );

    public abstract Task<string> ValidateAsync(
        PolicyUpdate policyUpdate,
        Policy? currentPolicy
    );
}
