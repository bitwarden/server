using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations.Interfaces;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrations;

public class UpdateOrganizationIntegrationCommand(
    IOrganizationIntegrationRepository integrationRepository,
    [FromKeyedServices(EventIntegrationsCacheConstants.CacheName)]
    IFusionCache cache)
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
            throw new NotFoundException();
        }

        updatedIntegration.Id = integration.Id;
        updatedIntegration.OrganizationId = integration.OrganizationId;
        updatedIntegration.CreationDate = integration.CreationDate;
        await integrationRepository.ReplaceAsync(updatedIntegration);
        await cache.RemoveByTagAsync(
            EventIntegrationsCacheConstants.BuildCacheTagForOrganizationIntegration(
                organizationId: organizationId,
                integrationType: integration.Type
            ));

        return updatedIntegration;
    }
}
