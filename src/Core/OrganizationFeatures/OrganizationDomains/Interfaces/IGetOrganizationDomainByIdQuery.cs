using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByIdQuery
{
    Task<OrganizationDomain> GetOrganizationDomainById(Guid domainId);
}
