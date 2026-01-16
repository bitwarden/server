using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyQuery(IPolicyRepository policyRepository) : IPolicyQuery
{
    public async Task<PolicyData> RunAsync(Guid organizationId, PolicyType policyType)
    {
        var dbPolicy = await policyRepository.GetByOrganizationIdTypeAsync(organizationId, policyType);
        return new PolicyData(organizationId, policyType, dbPolicy);
    }
}
