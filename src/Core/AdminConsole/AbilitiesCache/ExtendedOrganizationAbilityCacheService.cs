using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public class ExtendedOrganizationAbilityCacheService(
    [FromKeyedServices(OrganizationAbilityCacheConstants.CacheName)] IFusionCache cache,
    IOrganizationRepository organizationRepository)
    : IOrganizationAbilityCacheService
{
    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId)
    {
        var cacheKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(orgId);
        return await cache.GetOrSetAsync<OrganizationAbility?>(
            cacheKey,
            async _ => await organizationRepository.GetAbilityAsync(orgId));
    }

    public async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        var cacheKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(organization.Id);
        await cache.SetAsync<OrganizationAbility?>(cacheKey, new OrganizationAbility(organization));
    }

    public async Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        var cacheKey = OrganizationAbilityCacheConstants.BuildCacheKeyForOrganizationAbility(organizationId);
        await cache.RemoveAsync(cacheKey);
    }
}
