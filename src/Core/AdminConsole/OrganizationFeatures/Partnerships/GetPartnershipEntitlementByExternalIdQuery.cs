using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships;

public class GetPartnershipEntitlementByExternalIdQuery(
    IOrganizationPartnershipEntitlementRepository organizationPartnershipEntitlementRepository)
    : IGetPartnershipEntitlementByExternalIdQuery
{
    public async Task<CommandResult<OrganizationPartnershipEntitlement>> GetAsync(
        Guid organizationPartnershipId, string externalId)
    {
        var entitlement = await organizationPartnershipEntitlementRepository
            .GetByExternalIdAsync(organizationPartnershipId, externalId);
        if (entitlement is null)
        {
            return new EntitlementNotFound();
        }

        return entitlement;
    }
}
