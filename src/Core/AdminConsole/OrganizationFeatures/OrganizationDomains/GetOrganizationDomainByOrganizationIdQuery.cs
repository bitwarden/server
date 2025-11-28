using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class GetOrganizationDomainByOrganizationIdQuery : IGetOrganizationDomainByOrganizationIdQuery
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public GetOrganizationDomainByOrganizationIdQuery(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId)
        => await _organizationDomainRepository.GetDomainsByOrganizationIdAsync(orgId);
}
