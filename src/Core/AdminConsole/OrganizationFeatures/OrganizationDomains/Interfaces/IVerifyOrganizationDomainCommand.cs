using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IVerifyOrganizationDomainCommand
{
    Task<OrganizationDomain> UserVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain);
    Task<OrganizationDomain> SystemVerifyOrganizationDomainAsync(OrganizationDomain organizationDomain);
}
