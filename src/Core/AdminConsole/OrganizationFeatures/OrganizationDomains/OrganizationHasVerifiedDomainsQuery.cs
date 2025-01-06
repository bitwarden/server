using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class OrganizationHasVerifiedDomainsQuery(IOrganizationDomainRepository domainRepository) : IOrganizationHasVerifiedDomainsQuery
{
    public async Task<bool> HasVerifiedDomainsAsync(Guid orgId) =>
        (await domainRepository.GetDomainsByOrganizationIdAsync(orgId)).Any(od => od.VerifiedDate is not null);
}
