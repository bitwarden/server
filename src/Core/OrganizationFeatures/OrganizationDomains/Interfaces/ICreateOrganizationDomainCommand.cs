using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface ICreateOrganizationDomainCommand
{
    Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain);
}
