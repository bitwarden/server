using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

/// <summary>
/// Command interface for updating organization integration configurations.
/// </summary>
public interface IUpdateOrganizationIntegrationConfigurationCommand
{
    /// <summary>
    /// Updates an existing configuration for an organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration.</param>
    /// <param name="configurationId">The unique identifier of the configuration to update.</param>
    /// <param name="updatedConfiguration">The updated configuration data.</param>
    /// <returns>The updated configuration.</returns>
    /// <exception cref="Exceptions.NotFoundException">
    /// Thrown when the integration or the configuration does not exist,
    /// or the integration does not belong to the specified organization,
    /// or the configuration does not belong to the specified integration.</exception>
    /// <exception cref="Exceptions.BadRequestException">Thrown when the configuration or filters
    /// are invalid for the integration type.</exception>
    Task<OrganizationIntegrationConfiguration> UpdateAsync(Guid organizationId, Guid integrationId, Guid configurationId, OrganizationIntegrationConfiguration updatedConfiguration);
}
