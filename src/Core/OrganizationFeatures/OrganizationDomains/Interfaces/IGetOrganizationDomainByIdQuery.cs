using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IGetOrganizationDomainByIdAndOrganizationIdQuery
{
    Task<OrganizationDomain> GetOrganizationDomainByIdAndOrganizationIdAsync(Guid id, Guid organizationId);
}
