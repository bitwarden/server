using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

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
        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType
            ));
    }
}
