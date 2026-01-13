using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyQuery(IPolicyRepository policyRepository) : IPolicyQuery
{
    public async Task<Policy> GetByOrganizationIdAndType(Guid organizationId, PolicyType policyType)
        => await policyRepository.GetByOrganizationIdTypeAsync(organizationId, policyType)
           ?? new Policy { OrganizationId = organizationId, Type = policyType };
}
