#nullable enable

using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IRequirementFactory<IPolicyRequirement>> factories)
    : IPolicyRequirementQuery
{
    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Policy Requirement found for " + typeof(T));
        }

        var policyDetails = await GetPolicyDetails(userId);
        var policiesOfType = policyDetails.Where(p => factory.PolicyTypes.Contains(p.PolicyType));
        var filteredPolicies = factory.Filter(policiesOfType);
        var requirement = factory.Create(filteredPolicies);

        return requirement;
    }

    private Task<IEnumerable<PolicyDetails>> GetPolicyDetails(Guid userId) =>
        policyRepository.GetPolicyDetailsByUserId(userId);
}

