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
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var policyDetails = await GetPolicyDetails(userId, factory.PolicyType);
        var filteredPolicies = policyDetails
            .Where(p => p.PolicyType == factory.PolicyType)
            .Where(factory.Enforce);
        var requirement = factory.Create(filteredPolicies);
        return requirement;
    }

    public async Task<IEnumerable<Guid>> GetManyByOrganizationIdAsync<T>(Guid organizationId)
        where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var organizationPolicyDetails = await GetOrganizationPolicyDetails(organizationId, factory.PolicyType);

        var eligibleOrganizationUserIds = organizationPolicyDetails
            .Where(p => p.PolicyType == factory.PolicyType)
            .Where(factory.Enforce)
            .Select(p => p.OrganizationUserId)
            .ToList();

        return eligibleOrganizationUserIds;
    }

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetPolicyDetails(Guid userId, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType([userId], policyType);

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetOrganizationPolicyDetails(Guid organizationId, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
}
