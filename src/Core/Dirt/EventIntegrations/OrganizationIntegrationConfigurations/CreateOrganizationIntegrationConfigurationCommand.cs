using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Services;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrationConfigurations;

/// <summary>
/// Command implementation for creating organization integration configurations with validation and cache invalidation support.
/// </summary>
public class CreateOrganizationIntegrationConfigurationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache,
    IOrganizationIntegrationConfigurationValidator validator)
    : ICreateOrganizationIntegrationConfigurationCommand
{
    public async Task<OrganizationIntegrationConfiguration> CreateAsync(
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegrationConfiguration configuration)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration == null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }
        if (!validator.ValidateConfiguration(integration.Type, configuration))
        {
            throw new BadRequestException(
                $"Invalid Configuration and/or Filters for integration type {integration.Type}");
        }

        var created = await configurationRepository.CreateAsync(configuration);

        // Invalidate the cached configuration details
        // Even though this is a new record, the cache could hold a stale empty list for this
        if (created.EventType == null)
        {
            // Wildcard configuration - invalidate all cached results for this org/integration
            await cache.RemoveByTagAsync(
                EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                    organizationId: organizationId,
                    integrationType: integration.Type
                ));
        }
        else
        {
            // Specific event type - only invalidate that specific cache entry
            await cache.RemoveAsync(
                EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                    organizationId: organizationId,
                    integrationType: integration.Type,
                    eventType: created.EventType.Value
                ));
        }

        return created;
    }
}
