using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByIdOrganizationIdQuery
{
    Task<OrganizationDomain> GetOrganizationDomainByIdOrganizationIdAsync(Guid id, Guid organizationId);
}
