namespace Bit.Core.AdminConsole.Services;

public interface IOrganizationDomainService
{
    Task ValidateOrganizationsDomainAsync();
    Task OrganizationDomainMaintenanceAsync();
}
