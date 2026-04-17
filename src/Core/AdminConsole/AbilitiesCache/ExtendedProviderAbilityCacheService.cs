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

    public async Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId)
    {
        return await cache.GetOrSetAsync<ProviderAbility?>(
            $"{providerId}",
            async _ => await providerRepository.GetAbilityAsync(providerId)
        );
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
