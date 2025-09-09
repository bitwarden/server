using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyValidators;


/// <summary>
/// Please do not use this validator. We're currently in the process of refactoring our policy validator pattern.
/// This is a stop-gap solution for post-policy-save side effects, but it is not the long-term solution.
/// </summary>
public abstract class OrganizationPolicyValidator(IPolicyRepository policyRepository, IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
{
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
}
