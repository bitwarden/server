using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class GetOrganizationDomainByIdQuery : IGetOrganizationDomainByIdQuery
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public GetOrganizationDomainByIdQuery(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<OrganizationDomain> GetOrganizationDomainById(Guid domainId)
        => await _organizationDomainRepository.GetByIdAsync(domainId);
}
