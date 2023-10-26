using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByOrganizationIdQuery
{
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId);
}
