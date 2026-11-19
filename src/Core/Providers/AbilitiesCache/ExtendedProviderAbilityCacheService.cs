using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public class ExtendedProviderAbilityCacheService(
    [FromKeyedServices(ExtendedProviderAbilityCacheService.CacheName)] IFusionCache cache,
    IProviderRepository providerRepository)
    : IProviderAbilityCacheService
{
    public const string CacheName = "ProviderAbilities";

    public async Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        return await cache.GetOrSetAsync<ProviderAbility?>(
            $"{providerId}",
            async (_, _) => await providerRepository.GetAbilityAsync(providerId),
            token: cancellationToken
        );
    }

    public async Task<Dictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds, CancellationToken cancellationToken = default)
    {
        var tasks = providerIds
            .Distinct()
            .Select(providerId => GetProviderAbilityAsync(providerId, cancellationToken));

        var results = await Task.WhenAll(tasks);

        return results
            .Where(ability => ability != null)
            .ToDictionary(ability => ability!.Id, ability => ability!);
    }

    public async Task UpsertProviderAbilityAsync(Provider provider)
    {
        await cache.SetAsync($"{provider.Id}", new ProviderAbility(provider));
    }

    public async Task DeleteProviderAbilityAsync(Guid providerId)
    {
        await cache.RemoveAsync($"{providerId}");
    }
}
