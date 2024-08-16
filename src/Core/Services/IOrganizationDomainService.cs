namespace Bit.Core.Services;

public interface IOrganizationDomainService
{
    Task ValidateOrganizationsDomainAsync();
    Task OrganizationDomainMaintenanceAsync();
    /// <summary>
    /// Indicates if the organization has any verified domains.
    /// </summary>
    Task<bool> HasVerifiedDomainsAsync(Guid orgId);
}
