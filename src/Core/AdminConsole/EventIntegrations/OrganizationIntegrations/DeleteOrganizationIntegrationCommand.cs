using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;

/// <summary>
/// Command implementation for deleting organization integrations with cache invalidation support.
/// </summary>
public class DeleteOrganizationIntegrationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache)
    : IDeleteOrganizationIntegrationCommand
{
    public async Task DeleteAsync(Guid organizationId, Guid integrationId)
    {
        var integration = await integrationRepository.GetByIdAsync(integrationId);
        if (integration is null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        await integrationRepository.DeleteAsync(integration);
        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integration.Type
            ));
    }
}
