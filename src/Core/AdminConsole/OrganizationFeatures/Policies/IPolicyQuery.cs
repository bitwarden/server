using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyQuery
{
    Task<Policy> GetByOrganizationIdTypeAsync(Guid organizationId, PolicyType policyType);
}
