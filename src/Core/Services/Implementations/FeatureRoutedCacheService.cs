using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService(
    IVCurrentInMemoryApplicationCacheService inMemoryApplicationCacheService,
    IProviderAbilityCacheService providerAbilityCacheService,
    IFeatureService featureService)
    : IApplicationCacheService
{

    public Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync() =>
        inMemoryApplicationCacheService.GetProviderAbilitiesAsync();

    public async Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return await providerAbilityCacheService.GetProviderAbilityAsync(providerId);
        }

        (await GetProviderAbilitiesAsync([providerId])).TryGetValue(providerId, out var providerAbility);
        return providerAbility;
    }

    public async Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return await providerAbilityCacheService.GetProviderAbilitiesAsync(providerIds);
        }

        var allProviderAbilities = await inMemoryApplicationCacheService.GetProviderAbilitiesAsync();
        return providerIds
            .Distinct()
            .Where(allProviderAbilities.ContainsKey)
            .ToDictionary(id => id, id => allProviderAbilities[id]);
    }

    public Task UpsertProviderAbilityAsync(Provider provider)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return providerAbilityCacheService.UpsertProviderAbilityAsync(provider);
        }

        return inMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
    }

    public Task DeleteProviderAbilityAsync(Guid providerId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return providerAbilityCacheService.DeleteProviderAbilityAsync(providerId);
        }

        return inMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
    }
}
