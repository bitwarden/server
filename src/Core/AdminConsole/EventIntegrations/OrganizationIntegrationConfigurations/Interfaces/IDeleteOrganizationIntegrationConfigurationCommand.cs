namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

public interface IDeleteOrganizationIntegrationConfigurationCommand
{
    Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId);
}
