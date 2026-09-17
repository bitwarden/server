using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Enums;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

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

    /// <summary>
    /// Disables a configuration that is currently enabled, in a single write and without reading it first.
    /// </summary>
    Task<bool> DisableAsync(
        Guid organizationId,
        Guid id,
        DateTime disabledDate,
        IntegrationFailureCategory disabledReason);

    /// <summary>
    /// Re-enables every configuration under an integration. Credentials live on the integration, so fixing it is
    /// what recovers the configurations the breaker disabled.
    /// </summary>
    Task ClearDisabledByIntegrationAsync(Guid organizationIntegrationId);
}
