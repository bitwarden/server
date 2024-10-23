using Bit.Core.AdminConsole.Repositories;
using Org.BouncyCastle.Bcpg;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementsQuery
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IEnumerable<IPolicyRequirementDefinition<IPolicyRequirement>> _policyRequirementDefinitions;

    public PolicyRequirementsQuery()
    {
        // TODO: deps
    }


    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
    {
        var definition = _policyRequirementDefinitions.Single(def => def is IPolicyRequirementDefinition<T>);
        var policies = await _policyRepository.GetManyByUserIdAsync(userId);

        var enforceablePolicies = policies
            .Where(p => p.Type == definition.Type)
            .Where(p => definition.FilterPredicate(p));

        // Why is cast necessary? maybe because it could be a *different* IPolicyRequirement?
        return (T)definition.Reduce(enforceablePolicies);
    }
}
