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
        => (await GetAsync<T>([userId])).Single();

    public async Task<IEnumerable<T>> GetAsync<T>(IEnumerable<Guid> userIds) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var userIdList = userIds.ToList();

        var policyDetailsByUser = (await GetPolicyDetails(userIdList, factory.PolicyType))
            .Where(factory.Enforce)
            .ToLookup(l => l.UserId);

        var policyRequirements = userIdList.Select(u => factory.Create(policyDetailsByUser[u]));

        return policyRequirements;
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

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetPolicyDetails(IEnumerable<Guid> userIds, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByUserIdsAndPolicyType(userIds, policyType);

    private async Task<IEnumerable<OrganizationPolicyDetails>> GetOrganizationPolicyDetails(Guid organizationId, PolicyType policyType)
        => await policyRepository.GetPolicyDetailsByOrganizationIdAsync(organizationId, policyType);
}
