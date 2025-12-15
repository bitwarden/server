using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

/// <summary>
/// Command implementation for updating organization integration configurations with validation and cache invalidation support.
/// </summary>
public class UpdateOrganizationIntegrationConfigurationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache,
    IOrganizationIntegrationConfigurationValidator validator)
    : IUpdateOrganizationIntegrationConfigurationCommand
{
    public async Task<OrganizationIntegrationConfiguration> UpdateAsync(
        Guid organizationId,
        Guid integrationId,
        Guid configurationId,
        OrganizationIntegrationConfiguration updatedConfiguration)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }
        var configuration = await configurationRepository.GetByIdAsync(configurationId);
        if (configuration is null || configuration.OrganizationIntegrationId != integrationId)
        {
            throw new NotFoundException();
        }
        if (!validator.ValidateConfiguration(integration.Type, updatedConfiguration))
        {
            throw new BadRequestException($"Invalid Configuration and/or Filters for integration type {integration.Type}");
        }

        updatedConfiguration.Id = configuration.Id;
        updatedConfiguration.CreationDate = configuration.CreationDate;
        await configurationRepository.ReplaceAsync(updatedConfiguration);

        // If either old or new EventType is null (wildcard), invalidate all cached results
        // for the specific integration
        if (configuration.EventType == null || updatedConfiguration.EventType == null)
        {
            // Wildcard involved - invalidate all cached results for this org/integration
            await cache.RemoveByTagAsync(
                EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                    organizationId: organizationId,
                    integrationType: integration.Type
                ));

            return updatedConfiguration;
        }

        // Both are specific event types - invalidate specific cache entries
        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType.Value
            ));

        // If event type changed, also clear the new event type's cache
        if (configuration.EventType != updatedConfiguration.EventType)
        {
            await cache.RemoveAsync(
                EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                    organizationId: organizationId,
                    integrationType: integration.Type,
                    eventType: updatedConfiguration.EventType.Value
                ));
        }

        return updatedConfiguration;
    }
}
