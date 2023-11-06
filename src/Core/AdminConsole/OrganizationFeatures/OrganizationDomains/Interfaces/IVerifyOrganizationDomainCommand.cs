using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IVerifyOrganizationDomainCommand
{
    Task<OrganizationDomain> VerifyOrganizationDomainAsync(OrganizationDomain organizationDomain);
}
