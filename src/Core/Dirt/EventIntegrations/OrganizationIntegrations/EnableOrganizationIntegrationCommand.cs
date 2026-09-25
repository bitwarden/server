using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;

public class EnableOrganizationIntegrationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    IOrganizationIntegrationConfigurationRepository configurationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache,
    TimeProvider timeProvider,
    ILogger<EnableOrganizationIntegrationCommand> logger)
    : IEnableOrganizationIntegrationCommand
{
    public async Task<int> EnableAsync(Guid organizationId, Guid integrationId)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null || integration.OrganizationId != organizationId)
        {
            throw new BadRequestException("Integration not found for this organization.");
        }

        var reEnabled = await configurationRepository.ClearDisabledByIntegrationAsync(
            organizationId: organizationId,
            organizationIntegrationId: integration.Id,
            revisionDate: timeProvider.GetUtcNow().UtcDateTime);

        if (reEnabled == 0)
        {
            return 0;
        }

        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integration.Type
            ));

        logger.LogInformation(
            "Re-enabled {Count} integration configurations disabled by the circuit breaker. " +
            "OrganizationId: {OrgId}, IntegrationType: {IntegrationType}",
            reEnabled,
            organizationId,
            integration.Type);

        return reEnabled;
    }
}
