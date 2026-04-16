using Bit.Core.AdminConsole.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;
using static Bit.Core.AdminConsole.AbilitiesCache.ExtendedOrganizationAbilityCacheConstants;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public static class ExtendedOrganizationAbilityCacheConstants
{
    public const string CacheName = "OrganizationAbilities";
}

public class ExtendedOrganizationAbilityCacheService(
    [FromKeyedServices(CacheName)] IFusionCache cache,
    IOrganizationRepository organizationRepository)
    : IOrganizationAbilityCacheService
{
    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrSetAsync<OrganizationAbility?>(
            orgId.ToString(),
            async (_, _) => await organizationRepository.GetAbilityAsync(orgId),
            token: cancellationToken);
    }

    public async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds, CancellationToken cancellationToken = default)
    {
        var tasks = orgIds.Distinct().Select(async orgId => (orgId, ability: await GetOrganizationAbilityAsync(orgId, cancellationToken)));
        var results = await Task.WhenAll(tasks);
        return results
            .Where(r => r.ability != null)
            .ToDictionary(r => r.orgId, r => r.ability!);
    }

    public async Task UpsertOrganizationAbilityAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await cache.SetAsync<OrganizationAbility?>(organization.Id.ToString(), new OrganizationAbility(organization), token: cancellationToken);
    }

    public async Task DeleteOrganizationAbilityAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(organizationId.ToString(), token: cancellationToken);
    }
}
