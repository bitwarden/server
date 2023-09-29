using Bit.Core.Entities;
using Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains;

public class GetOrganizationDomainByIdAndOrganizationIdQuery : IGetOrganizationDomainByIdAndOrganizationIdQuery
{
    private readonly IOrganizationDomainRepository _organizationDomainRepository;

    public GetOrganizationDomainByIdAndOrganizationIdQuery(IOrganizationDomainRepository organizationDomainRepository)
    {
        _organizationDomainRepository = organizationDomainRepository;
    }

    public async Task<OrganizationDomain> GetOrganizationDomainByIdAndOrganizationIdAsync(Guid id, Guid organizationId)
        => await _organizationDomainRepository.GetDomainByIdAndOrganizationIdAsync(id, organizationId);
}
