using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services.Implementations;

/// <summary>
/// A feature-flagged routing service for application caching that bridges the gap between
/// scoped dependency injection (IFeatureService) and singleton services (cache implementations).
/// This service allows dynamic routing between IVCurrentInMemoryApplicationCacheService and
/// IVNextInMemoryApplicationCacheService based on the PM23845_VNextApplicationCache feature flag.
/// </summary>
/// <remarks>
/// This service is necessary because:
/// - IFeatureService is registered as Scoped in the DI container
/// - IVNextInMemoryApplicationCacheService and IVCurrentInMemoryApplicationCacheService are registered as Singleton
/// - We need to evaluate feature flags at request time while maintaining singleton cache behavior
///
/// The service acts as a scoped proxy that can access the scoped IFeatureService while
/// delegating actual cache operations to the appropriate singleton implementation.
/// </remarks>
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

        return await inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
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
