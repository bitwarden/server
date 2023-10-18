using Bit.Core.Entities;

namespace Bit.Core.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IDeleteOrganizationDomainCommand
{
    Task DeleteAsync(OrganizationDomain organizationDomain);
}
