using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface ICreateOrganizationDomainCommand
{
    Task<OrganizationDomain> CreateAsync(OrganizationDomain organizationDomain);
}
