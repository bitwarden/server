// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class InMemoryApplicationCacheService : IApplicationCacheService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private readonly IVNextInMemoryApplicationCacheService _vNextInMemoryApplicationCacheService;
    private readonly bool _useVNext;
    private DateTime _lastOrgAbilityRefresh = DateTime.MinValue;
    private IDictionary<Guid, OrganizationAbility> _orgAbilities;
    private TimeSpan _orgAbilitiesRefreshInterval = TimeSpan.FromMinutes(10);

    private IDictionary<Guid, ProviderAbility> _providerAbilities;

    public InMemoryApplicationCacheService(IOrganizationRepository organizationRepository,
        IProviderRepository providerRepository,
        IVNextInMemoryApplicationCacheService vNextInMemoryApplicationCacheService,
        bool useVNext = false)
    {
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
        _vNextInMemoryApplicationCacheService = vNextInMemoryApplicationCacheService;
        _useVNext = useVNext;
    }

    public virtual async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        if (_useVNext)
        {
            return await _vNextInMemoryApplicationCacheService.GetOrganizationAbilitiesAsync();
        }

        await InitOrganizationAbilitiesAsync();
        return _orgAbilities;
    }

#nullable enable
    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid organizationId)
    {
        if (_useVNext)
        {
            return await _vNextInMemoryApplicationCacheService.GetOrganizationAbilityAsync(organizationId);
        }

        (await GetOrganizationAbilitiesAsync())
            .TryGetValue(organizationId, out var organizationAbility);
        return organizationAbility;
    }
#nullable disable

    public virtual async Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        if (_useVNext)
        {
            return await _vNextInMemoryApplicationCacheService.GetProviderAbilitiesAsync();
        }

        await InitProviderAbilitiesAsync();
        return _providerAbilities;
    }

    public virtual async Task UpsertProviderAbilityAsync(Provider provider)
    {
        if (_useVNext)
        {
            await _vNextInMemoryApplicationCacheService.UpsertProviderAbilityAsync(provider);
            return;
        }

        await InitProviderAbilitiesAsync();
        var newAbility = new ProviderAbility(provider);

        _providerAbilities[provider.Id] = newAbility;
    }

    public virtual async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        if (_useVNext)
        {
            await _vNextInMemoryApplicationCacheService.UpsertOrganizationAbilityAsync(organization);
            return;
        }

        await InitOrganizationAbilitiesAsync();
        var newAbility = new OrganizationAbility(organization);

        _orgAbilities[organization.Id] = newAbility;
    }

    public virtual Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        if (_useVNext)
        {
            return _vNextInMemoryApplicationCacheService.DeleteOrganizationAbilityAsync(organizationId);
        }

        _orgAbilities?.Remove(organizationId);

        return Task.FromResult(0);
    }

    public virtual Task DeleteProviderAbilityAsync(Guid providerId)
    {
        if (_useVNext)
        {
            return _vNextInMemoryApplicationCacheService.DeleteProviderAbilityAsync(providerId);
        }

        _providerAbilities?.Remove(providerId);

        return Task.FromResult(0);
    }

    private async Task InitOrganizationAbilitiesAsync()
    {
        var now = DateTime.UtcNow;
        if (_orgAbilities == null || (now - _lastOrgAbilityRefresh) > _orgAbilitiesRefreshInterval)
        {
            var abilities = await _organizationRepository.GetManyAbilitiesAsync();
            _orgAbilities = abilities.ToDictionary(a => a.Id);
            _lastOrgAbilityRefresh = now;
        }
    }

    private async Task InitProviderAbilitiesAsync()
    {
        var now = DateTime.UtcNow;
        if (_providerAbilities == null || (now - _lastOrgAbilityRefresh) > _orgAbilitiesRefreshInterval)
        {
            var abilities = await _providerRepository.GetManyAbilitiesAsync();
            _providerAbilities = abilities.ToDictionary(a => a.Id);
            _lastOrgAbilityRefresh = now;
        }
    }
}
