namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;

public interface IDeleteOrganizationIntegrationCommand
{
    Task DeleteAsync(Guid organizationId, Guid integrationId);
}
