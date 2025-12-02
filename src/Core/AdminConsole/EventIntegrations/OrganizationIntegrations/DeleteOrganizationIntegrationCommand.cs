using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations;

public class DeleteOrganizationIntegrationCommand : IDeleteOrganizationIntegrationCommand
{
    private readonly IOrganizationIntegrationRepository _integrationRepository;
    private readonly IFusionCache _cache;

    public DeleteOrganizationIntegrationCommand(
        IOrganizationIntegrationRepository integrationRepository,
        [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)] IFusionCache cache)
    {
        _integrationRepository = integrationRepository;
        _cache = cache;
    }

    public async Task DeleteAsync(Guid organizationId, Guid integrationId)
    {
        var integration = await _integrationRepository.GetByIdAsync(integrationId);
        if (integration is null || integration.OrganizationId != organizationId)
        {
            throw new NotFoundException();
        }

        await _integrationRepository.DeleteAsync(integration);
        await _cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integration.Type
            ));
    }
}
