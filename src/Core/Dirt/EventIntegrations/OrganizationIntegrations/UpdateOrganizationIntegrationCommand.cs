using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;

/// <summary>
/// Command implementation for updating organization integrations with cache invalidation support.
/// </summary>
public class UpdateOrganizationIntegrationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache,
    ILogger<UpdateOrganizationIntegrationCommand> logger)
    : IUpdateOrganizationIntegrationCommand
{
    public async Task<OrganizationIntegration> UpdateAsync(
        Guid organizationId,
        Guid integrationId,
        OrganizationIntegration updatedIntegration)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null ||
            integration.OrganizationId != organizationId ||
            integration.Type != updatedIntegration.Type)
        {
            throw new BadRequestException("Integration not found for this organization with the specified type.");
        }

        updatedIntegration.Id = integration.Id;
        updatedIntegration.OrganizationId = integration.OrganizationId;
        updatedIntegration.CreationDate = integration.CreationDate;

        await integrationRepository.ReplaceAsync(updatedIntegration);

        // Credentials live on the integration, so fixing it is what recovers the configurations the breaker
        // disabled underneath it
        var reEnabled = await configurationRepository.ClearDisabledByIntegrationAsync(
            organizationId: organizationId,
            organizationIntegrationId: integration.Id,
            revisionDate: updatedIntegration.RevisionDate);
        if (reEnabled > 0)
        {
            logger.LogInformation(
                "Re-enabled {Count} integration configurations disabled by the circuit breaker. " +
                "OrganizationId: {OrgId}, IntegrationType: {IntegrationType}",
                reEnabled,
                organizationId,
                integration.Type);
        }
        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integration.Type
            ));

        return updatedIntegration;
    }
}
