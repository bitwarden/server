using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirementQueries;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(IPolicyRepository policyRepository) : IPolicyRequirementQuery
{
    public async Task<SingleOrganizationRequirement> GetSingleOrganizationRequirementAsync(Guid userId)
        => new(await GetPolicyDetails(userId));

    public async Task<SendRequirement> GetSendRequirementAsync(Guid userId)
        => SendRequirement.Create(await GetPolicyDetails(userId));

    private Task<IEnumerable<OrganizationUserPolicyDetails>> GetPolicyDetails(Guid userId) =>
        policyRepository.GetPolicyDetailsByUserId(userId);
}
