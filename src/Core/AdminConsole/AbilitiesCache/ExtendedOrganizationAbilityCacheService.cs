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

    /// <summary>
    /// Version segment prefixed to every cache key. Cached <see cref="OrganizationAbility"/> entries are
    /// System.Text.Json documents in the shared L2 cache, so a property added to the model is read back as
    /// <c>false</c> from any entry written before the property existed. Bump this whenever a new property's
    /// default would wrongly take access away (for example <see cref="OrganizationAbility.UseSecretsManager"/>),
    /// so that a new build never reads entries written by an older one, including during a rolling deploy.
    /// </summary>
    public const string KeyVersion = "v2";
}

public class ExtendedOrganizationAbilityCacheService(
    [FromKeyedServices(CacheName)] IFusionCache cache,
    IOrganizationRepository organizationRepository)
    : IOrganizationAbilityCacheService
{
    private static string CacheKey(Guid organizationId) => $"{KeyVersion}:{organizationId}";

    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrSetAsync<OrganizationAbility?>(
            CacheKey(orgId),
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
        await cache.SetAsync<OrganizationAbility?>(CacheKey(organization.Id), new OrganizationAbility(organization), token: cancellationToken);
    }

    public async Task DeleteOrganizationAbilityAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        await cache.RemoveAsync(CacheKey(organizationId), token: cancellationToken);
    }
}
