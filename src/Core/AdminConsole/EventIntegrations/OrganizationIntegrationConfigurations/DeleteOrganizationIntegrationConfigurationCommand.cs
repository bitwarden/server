using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

/// <summary>
/// Command implementation for deleting organization integration configurations with cache invalidation support.
/// </summary>
public class DeleteOrganizationIntegrationConfigurationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache)
    : IDeleteOrganizationIntegrationConfigurationCommand
{
    public async Task DeleteAsync(Guid organizationId, Guid integrationId, Guid configurationId)
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

        await configurationRepository.DeleteAsync(configuration);

        if (configuration.EventType == null)
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
                    eventType: configuration.EventType.Value
                ));
        }
    }
}
