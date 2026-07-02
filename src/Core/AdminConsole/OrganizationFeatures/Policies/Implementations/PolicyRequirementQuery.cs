using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IPolicyRequirementQuery
{
    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
        => (await GetAsync<T>([userId])).Single().Requirement;

    public async Task<T> GetAsyncVNext<T>(Guid userId) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        IEnumerable<PolicyDetails> policyDetails;
        if (factory.DefaultState == PolicyDefaultState.Enabled)
        {
            // Policies that are enabled by default read the raw state and treat "no row" the same as enabled;
            // an explicitly disabled row (Enabled == false) is excluded.
            var policyDetailsWithState =
                await policyRepository.GetPolicyDetailsWithStateByUserIdAndPolicyTypeAsync(userId, factory.PolicyType);
            policyDetails = policyDetailsWithState.Where(x => x.Enabled != false);
        }
        else
        {
            policyDetails = await policyRepository.GetPolicyDetailsByUserIdAndPolicyTypeAsync(userId, factory.PolicyType);
        }

        return factory.Create(policyDetails.Where(factory.Enforce));
    }

    public async Task<IEnumerable<(Guid UserId, T Requirement)>> GetAsync<T>(IEnumerable<Guid> userIds) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        if (factory.DefaultState == PolicyDefaultState.Enabled)
        {
            // The batch query does not yet support enabled-by-default policies (it would miss organizations with no
            // policy row). Use GetAsyncVNext for these until a batch state-aware query exists.
            throw new NotImplementedException(
                "Enabled-by-default policies must be queried via GetAsyncVNext, not the batch overload: " + typeof(T));
        }

        var userIdList = userIds.ToList();

        var policyDetailsByUser = (await GetPolicyDetails(userIdList, factory.PolicyType))
            .Where(factory.Enforce)
            .ToLookup(l => l.UserId);

        var policyRequirements = userIdList.Select(u => (u, factory.Create(policyDetailsByUser[u])));

        return policyRequirements;
    }

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetPolicyDetails(IEnumerable<Guid> userIds, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(userIds, policyType);
}
