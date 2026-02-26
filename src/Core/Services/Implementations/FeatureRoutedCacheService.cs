using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService(
    IVCurrentInMemoryApplicationCacheService inMemoryApplicationCacheService)
    : IApplicationCacheService
{
    public Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync() =>
        inMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();

    public Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId) =>
        inMemoryApplicationCacheService.GetOrganizationAbilityAsync(orgId);

    public Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync() =>
        inMemoryApplicationCacheService.GetProviderAbilitiesAsync();

    public Task UpsertOrganizationAbilityAsync(Organization organization) =>
        inMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);

    public Task UpsertProviderAbilityAsync(Provider provider) =>
        inMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);

    public Task DeleteOrganizationAbilityAsync(Guid organizationId) =>
        inMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);

    public Task DeleteProviderAbilityAsync(Guid providerId) =>
        inMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);

    public async Task BaseUpsertOrganizationAbilityAsync(Organization organization)
    {
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
