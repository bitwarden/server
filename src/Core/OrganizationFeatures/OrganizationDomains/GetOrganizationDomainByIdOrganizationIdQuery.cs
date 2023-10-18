using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class GetOrganizationDomainByIdOrganizationIdQuery : IGetOrganizationDomainByIdOrganizationIdQuery
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public GetOrganizationDomainByIdOrganizationIdQuery(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<OrganizationDomain> GetOrganizationDomainByIdOrganizationIdAsync(Guid id, Guid organizationId)
        => await _organizationDomainRepository.GetDomainByIdOrganizationIdAsync(id, organizationId);
}
