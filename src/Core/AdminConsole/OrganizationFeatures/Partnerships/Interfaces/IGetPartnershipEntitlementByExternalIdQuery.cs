using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Partnerships.Interfaces;

public interface IGetPartnershipEntitlementByExternalIdQuery
{
    /// <returns>The entitlement, or <see cref="EntitlementNotFound"/>.</returns>
    Task<CommandResult<OrganizationPartnershipEntitlement>> GetAsync(Guid organizationPartnershipId, string externalId);
}
