using Bit.Core.AdminConsole.Entities;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;

/// <summary>
/// Query interface for retrieving organization integration configurations.
/// </summary>
public interface IGetOrganizationIntegrationConfigurationsQuery
{
    /// <summary>
    /// Retrieves all configurations for a specific organization integration.
    /// </summary>
    /// <param name="organizationId">The unique identifier of the organization.</param>
    /// <param name="integrationId">The unique identifier of the integration.</param>
    /// <returns>A list of configurations associated with the integration.</returns>
    /// <exception cref="Exceptions.NotFoundException">Thrown when the integration does not exist
    /// or does not belong to the specified organization.</exception>
    Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(Guid organizationId, Guid integrationId);
}
