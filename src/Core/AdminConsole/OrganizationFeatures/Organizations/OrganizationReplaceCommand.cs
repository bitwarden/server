using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.Organizations.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Organizations;

public class OrganizationReplaceCommand(
    IOrganizationRepository organizationRepository,
    IOrganizationAbilityCacheService organizationAbilityCacheService,
    IEventService eventService,
    ILogger<OrganizationReplaceCommand> logger) : IOrganizationReplaceCommand
{
    public async Task ReplaceAndUpdateCacheAsync(Organization org, EventType? orgEvent = null)
    {
        try
        {
            await organizationRepository.ReplaceAsync(org);
            await organizationAbilityCacheService.UpsertOrganizationAbilityAsync(org);

            if (orgEvent.HasValue)
            {
                await eventService.LogOrganizationEventAsync(org, orgEvent.Value);
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "An error occurred while calling {Method} for Organization ({OrganizationID})", nameof(ReplaceAndUpdateCacheAsync), org.Id);
            throw;
        }
    }
}
