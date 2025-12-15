using Bit.Core.AdminConsole.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Repositories;

public interface IOrganizationIntegrationConfigurationRepository : IRepository<OrganizationIntegrationConfiguration, Guid>
{
    /// <summary>
    /// Retrieve the list of available configuration details for a specific event for the organization and
    /// integration type.<br/>
    /// <br/>
    /// <b>Note:</b> This returns all configurations that match the event type explicitly <b>and</b>
    /// all the configurations that have a null event type - null event type is considered a
    /// wildcard that matches all events.
    ///
    /// </summary>
    /// <param name="eventType">The specific event type</param>
    /// <param name="organizationId">The id of the organization</param>
    /// <param name="integrationType">The integration type</param>
    /// <returns>A List of <see cref="OrganizationIntegrationConfigurationDetails"/> that match</returns>
    Task<List<OrganizationIntegrationConfigurationDetails>> GetManyByEventTypeOrganizationIdIntegrationType(
        EventType eventType,
        Guid organizationId,
        IntegrationType integrationType);

    Task<List<OrganizationIntegrationConfigurationDetails>> GetAllConfigurationDetailsAsync();

    Task<List<OrganizationIntegrationConfiguration>> GetManyByIntegrationAsync(Guid organizationIntegrationId);
}
