using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public interface IPolicyQuery
{
    Task<Policy> GetByOrganizationIdAndType(Guid organizationId, PolicyType policyType);
}
