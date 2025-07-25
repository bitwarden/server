using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository, IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories) : IPolicyValidator
{
    private readonly IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> _factories = factories;

    public abstract PolicyType Type { get; }

    public abstract IEnumerable<PolicyType> RequiredPolicies { get; }


    protected async Task<IEnumerable<T>> GetUserPolicyRequirementsByOrganizationIdAsync<T>(Guid organizationId, PolicyType policyType) where T : IPolicyRequirement
    {
        var policyDetails = await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
        var policyDetailGroups = policyDetails.GroupBy(policyDetail => policyDetail.UserId);

        var requirements = new List<T>();

        foreach (var policyDetailGroup in policyDetailGroups)
        {
            requirements.Add(PolicyRequirementBuilder.Build<T>(_factories, policyDetailGroup.ToList()));

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
