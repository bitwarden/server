using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.Dirt.EventIntegrations.OrganizationIntegrations;

/// <summary>
/// Command implementation for creating organization integrations with cache invalidation support.
/// </summary>
public class CreateOrganizationIntegrationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache)
    : ICreateOrganizationIntegrationCommand
{
    public async Task<OrganizationIntegration> CreateAsync(OrganizationIntegration integration)
    {
        var existingIntegrations = await integrationRepository
            .GetManyByOrganizationAsync(integration.OrganizationId);
        if (existingIntegrations.Any(i => i.Type == integration.Type))
        {
            throw new BadRequestException("An integration of this type already exists for this organization.");
        }

        var created = await integrationRepository.CreateAsync(integration);
        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: integration.OrganizationId,
                integrationType: integration.Type
            ));

        return created;
    }
}
