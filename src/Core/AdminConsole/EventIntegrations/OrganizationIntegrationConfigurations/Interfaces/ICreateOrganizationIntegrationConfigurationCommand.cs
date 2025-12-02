using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

public interface ICreateOrganizationIntegrationConfigurationCommand
{
    Task<OrganizationIntegrationConfiguration> CreateAsync(Guid organizationId, Guid integrationId, OrganizationIntegrationConfiguration configuration);
}
