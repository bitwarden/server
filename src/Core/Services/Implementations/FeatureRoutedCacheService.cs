using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService : IFeatureRoutedCacheService
{
    private readonly IFeatureService _featureService;
    private readonly IVNextInMemoryApplicationCacheService _vNextInMemoryApplicationCacheService;
    private readonly InMemoryApplicationCacheService _inMemoryApplicationCacheService;

    public FeatureRoutedCacheService(
        IFeatureService featureService,
        IVNextInMemoryApplicationCacheService vNextInMemoryApplicationCacheService,
        InMemoryApplicationCacheService inMemoryApplicationCacheService)
    {
        _featureService = featureService;
        _vNextInMemoryApplicationCacheService = vNextInMemoryApplicationCacheService;
        _inMemoryApplicationCacheService = inMemoryApplicationCacheService;
    }

    public Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.GetOrganizationAbilitiesAsync()
            : _inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
    }

    public Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId)
            : _inMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId);
    }

    public Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.GetProviderAbilitiesAsync()
            : _inMemoryApplicationCacheService.GetProviderAbilitiesAsync();
    }

    public Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization)
            : _inMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);
    }

    public Task UpsertProviderAbilityAsync(Provider provider)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider)
            : _inMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
    }

    public Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId)
            : _inMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);
    }

    public Task DeleteProviderAbilityAsync(Guid providerId)
    {
        return _featureService.IsEnabled(FeatureFlagKeys.PM23845_VNextApplicationCache)
            ? _vNextInMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId)
            : _inMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
    }
}
