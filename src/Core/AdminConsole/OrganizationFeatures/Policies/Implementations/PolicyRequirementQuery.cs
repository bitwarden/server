#nullable enable

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
    {
        var policyDetails = await GetPolicyDetails(userId);

        return PolicyRequirementBuilder.Build<T>(factories, policyDetails);
    }

    private Task<IEnumerable<PolicyDetails>> GetPolicyDetails(Guid userId)
        => policyRepository.GetPolicyDetailsByUserId(userId);
}
