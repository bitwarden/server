using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByIdOrganizationIdQuery
{
    Task<OrganizationDomain> GetOrganizationDomainByIdOrganizationIdAsync(Guid id, Guid organizationId);
}
