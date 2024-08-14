using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByOrganizationIdQuery
{
    Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId);
}
