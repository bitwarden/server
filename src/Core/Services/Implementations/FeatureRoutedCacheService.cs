using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService(
    IVCurrentInMemoryApplicationCacheService inMemoryApplicationCacheService,
    IOrganizationAbilityCacheService extendedCacheService,
    IProviderAbilityCacheService providerAbilityCacheService,
    IFeatureService featureService)
    : IApplicationCacheService
{
    public Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync() =>
        inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();

    public Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId) =>
        featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            ? extendedCacheService.GetOrganizationAbilityAsync(orgId)
            : inMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId);

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
        var allProviderAbilities = await inMemoryApplicationCacheService.GetProviderAbilitiesAsync();
        return providerIds
            .Distinct()
            .Where(allProviderAbilities.ContainsKey)
            .ToDictionary(id => id, id => allProviderAbilities[id]);
    }

    public async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds)
    {
        var allOrganizationAbilities = await inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
        return orgIds
            .Distinct()
            .Where(allOrganizationAbilities.ContainsKey)
            .ToDictionary(id => id, id => allOrganizationAbilities[id]);
    }

    public Task UpsertOrganizationAbilityAsync(Organization organization) =>
        featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            ? extendedCacheService.UpsertOrganizationAbilityAsync(organization)
            : inMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);

    public Task UpsertProviderAbilityAsync(Provider provider)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return providerAbilityCacheService.UpsertProviderAbilityAsync(provider);
        }

        return inMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
    }

    public Task DeleteOrganizationAbilityAsync(Guid organizationId) =>
        featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache)
            ? extendedCacheService.DeleteOrganizationAbilityAsync(organizationId)
            : inMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);

    public Task DeleteProviderAbilityAsync(Guid providerId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.ProviderAbilityExtendedCache))
        {
            return providerAbilityCacheService.DeleteProviderAbilityAsync(providerId);
        }

        return inMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
    }

    public async Task BaseUpsertOrganizationAbilityAsync(Organization organization)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache))
        {
            await extendedCacheService.UpsertOrganizationAbilityAsync(organization);
            return;
        }

        if (inMemoryApplicationCacheService is InMemoryServiceBusApplicationCacheService serviceBusCache)
        {
            await serviceBusCache.BaseUpsertOrganizationAbilityAsync(organization);
        }
        else
        {
            throw new InvalidOperationException($"Expected {nameof(inMemoryApplicationCacheService)} to be of type {nameof(InMemoryServiceBusApplicationCacheService)}");
        }
    }

    public async Task BaseDeleteOrganizationAbilityAsync(Guid organizationId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.OrgAbilityExtendedCache))
        {
            await extendedCacheService.DeleteOrganizationAbilityAsync(organizationId);
            return;
        }

        if (inMemoryApplicationCacheService is InMemoryServiceBusApplicationCacheService serviceBusCache)
        {
            await serviceBusCache.BaseDeleteOrganizationAbilityAsync(organizationId);
        }
        else
        {
            throw new InvalidOperationException($"Expected {nameof(inMemoryApplicationCacheService)} to be of type {nameof(InMemoryServiceBusApplicationCacheService)}");
        }
    }
}
