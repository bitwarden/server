using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.Services;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;

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

        await cache.RemoveAsync(
            EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                organizationId: organizationId,
                integrationType: integration.Type,
                eventType: configuration.EventType
            ));

        // Clear both event type configurations to make sure all stale records are removed
        if (configuration.EventType != updatedConfiguration.EventType)
        {
            await cache.RemoveAsync(
                EventIntegrationsCacheConstants.BuildCacheKeyForOrganizationIntegrationConfigurationDetails(
                    organizationId: organizationId,
                    integrationType: integration.Type,
                    eventType: updatedConfiguration.EventType
                ));
        }

        return updatedConfiguration;
    }
}
