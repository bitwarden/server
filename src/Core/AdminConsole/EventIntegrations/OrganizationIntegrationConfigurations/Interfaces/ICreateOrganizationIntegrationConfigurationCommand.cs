using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

/// <summary>
/// Command interface for creating organization integration configurations.
/// </summary>
public interface ICreateOrganizationIntegrationConfigurationCommand
{
    /// <summary>
    /// Creates a new configuration for an organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration.</param>
    /// <param name="configuration">The configuration to create.</param>
    /// <returns>The created configuration.</returns>
    /// <exception cref="Exceptions.NotFoundException">Thrown when the integration does not exist
    /// or does not belong to the specified organization.</exception>
    /// <exception cref="Exceptions.BadRequestException">Thrown when the configuration or filters
    /// are invalid for the integration type.</exception>
    Task<OrganizationIntegrationConfiguration> CreateAsync(Guid organizationId, Guid integrationId, OrganizationIntegrationConfiguration configuration);
}
