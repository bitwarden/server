using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService(
    IFeatureService featureService,
    IVNextInMemoryApplicationCacheService vNextInMemoryApplicationCacheService,
    IVCurrentInMemoryApplicationCacheService inMemoryApplicationCacheService)
    : IApplicationCacheService
{
    public async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            return await vNextInMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
        }
        else
        {
            return await inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
        }
    }

    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            return await vNextInMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId);
        }
        return await inMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId);
    }

    public async Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            return await vNextInMemoryApplicationCacheService.GetProviderAbilitiesAsync();
        }
        return await inMemoryApplicationCacheService.GetProviderAbilitiesAsync();
    }

    public async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            await vNextInMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
        else
        {
            await inMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);
        }
    }

    public async Task UpsertProviderAbilityAsync(Provider provider)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            await vNextInMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
        }
        else
        {
            await inMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
        }
    }

    public async Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            await vNextInMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);
        }
        else
        {
            await inMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);
        }
    }

    public async Task DeleteProviderAbilityAsync(Guid providerId)
    {
        if (featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache))
        {
            await vNextInMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
        }
        else
        {
            await inMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
        }
    }
}
