namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;

public interface IOrganizationHasVerifiedDomainsQuery
{
    Task<bool> HasVerifiedDomainsAsync(Guid orgId);
}
