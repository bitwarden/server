using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

public interface IUpdateOrganizationIntegrationConfigurationCommand
{
    Task<OrganizationIntegrationConfiguration> UpdateAsync(Guid organizationId, Guid integrationId, Guid configurationId, OrganizationIntegrationConfiguration updatedConfiguration);
}
