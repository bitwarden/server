using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;

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
