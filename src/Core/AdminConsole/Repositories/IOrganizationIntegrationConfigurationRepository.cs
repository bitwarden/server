using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository : IRepository<OrganizationIntegrationConfiguration, Guid>
{
    /// <summary>
    /// Retrieve the list of available configuration details for a specific event for the organization and
    /// integration type.
    /// </summary>
    /// <param name="organizationId">The id of the organization</param>
    /// <param name="integrationType">The integration type</param>
    /// <param name="eventType">The specific event type</param>
    /// <returns>A List of <see cref="OrganizationIntegrationConfigurationDetails"/> that match</returns>
    Task<List<OrganizationIntegrationConfigurationDetails>> GetConfigurationDetailsAsync(
        Guid organizationId,
        IntegrationType integrationType,
        EventType eventType);

    /// <summary>
    /// Retrieve the list of configuration details for the organization and
    /// integration type that have a null event type (i.e. match all events).
    /// </summary>
    /// <param name="organizationId">The id of the organization</param>
    /// <param name="integrationType">The integration type</param>
    /// <returns>A List of <see cref="OrganizationIntegrationConfigurationDetails"/> that match</returns>
    Task<List<OrganizationIntegrationConfigurationDetails>> GetManyConfigurationDetailsByOrganizationIdIntegrationTypeAsync(
        Guid organizationId,
        IntegrationType integrationType);

    Task<List<OrganizationIntegrationConfigurationDetails>> GetAllConfigurationDetailsAsync();

    Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(Guid organizationIntegrationId);
}
