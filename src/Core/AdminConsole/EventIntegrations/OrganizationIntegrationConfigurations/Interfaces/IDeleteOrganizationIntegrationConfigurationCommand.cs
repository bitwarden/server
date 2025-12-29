namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

/// <summary>
/// Command interface for deleting organization integration configurations.
/// </summary>
public interface IDeleteOrganizationIntegrationConfigurationCommand
{
    /// <summary>
    /// Deletes a configuration from an organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration.</param>
    /// <param name="configurationId">The unique identifier of the configuration to delete.</param>
    /// <exception cref="Exceptions.NotFoundException">
    /// Thrown when the integration or configuration does not exist,
    /// or the integration does not belong to the specified organization,
    /// or the configuration does not belong to the specified integration.</exception>
    Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId);
}
