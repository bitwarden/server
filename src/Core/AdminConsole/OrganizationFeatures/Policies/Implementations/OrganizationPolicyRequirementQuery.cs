#nullable enable

using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class OrganizationPolicyRequirementQuery(
    IPolicyRepository policyRepository,
    IEnumerable<IOrganizationPolicyRequirementFactory<IPolicyRequirement>> factories)
    : IOrganizationPolicyRequirementQuery
{
    public async Task<T> GetAsync<T>(Guid organizationId) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IOrganizationPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Organization Requirement Factory found for " + typeof(T));
        }

        var policy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, factory.PolicyType);

        var requirement = factory.Create(policy);
        return requirement;
    }
}
