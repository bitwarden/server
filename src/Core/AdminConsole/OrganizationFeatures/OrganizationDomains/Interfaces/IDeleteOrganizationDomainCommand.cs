using Bit.Core.Entities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IDeleteOrganizationDomainCommand
{
    Task DeleteAsync(OrganizationDomain organizationDomain);
}
