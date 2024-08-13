namespace Bit.Core.Services;

public interface IOrganizationDomainService
{
    Task ValidateOrganizationsDomainAsync();
    Task OrganizationDomainMaintenanceAsync();
    Task<bool> HasVerifiedDomainsAsync(Guid orgId);
}
