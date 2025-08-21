using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;

public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository, IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories) : IPolicyValidator
{
    public abstract PolicyType Type { get; }

    public abstract IEnumerable<PolicyType> RequiredPolicies { get; }

    protected async Task<IEnumerable<T>> GetUserPolicyRequirementsByOrganizationIdAsync<T>(Guid organizationId, PolicyType policyType) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var policyDetails = await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
        var policyDetailGroups = policyDetails.GroupBy(policyDetail => policyDetail.UserId);
        var requirements = new List<T>();

        foreach (var policyDetailGroup in policyDetailGroups)
        {
            var filteredPolicies = policyDetailGroup
                .Where(factory.Enforce)
                // Prevent deferred execution from causing inconsistent tests.
                .ToList();

            requirements.Add(factory.Create(filteredPolicies));
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

    public abstract Task ProtoTypeOnSaveSideEffectsAsync(
        SavePolicyModel policyModel,
        Policy? currentPolicy
    );
}
