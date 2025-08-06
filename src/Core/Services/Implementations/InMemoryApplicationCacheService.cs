// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Collections.Concurrent;
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
    private DateTime _lastOrgAbilityRefresh = DateTime.MinValue;
    private DateTime _lastProviderAbilityRefresh = DateTime.MinValue;
    private ConcurrentDictionary<Guid, OrganizationAbility> _orgAbilities;
    private TimeSpan _orgAbilitiesRefreshInterval = TimeSpan.FromMinutes(10);
    private readonly SemaphoreSlim _orgInitLock = new(1, 1);
    private readonly SemaphoreSlim _providerInitLock = new(1, 1);

    private ConcurrentDictionary<Guid, ProviderAbility> _providerAbilities;

    public InMemoryApplicationCacheService(
        IOrganizationRepository organizationRepository, IProviderRepository providerRepository)
    {
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
    }

    public virtual async Task<ConcurrentDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        await InitOrganizationAbilitiesAsync();
        return _orgAbilities;
    }

#nullable enable
    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid organizationId)
    {
        (await GetOrganizationAbilitiesAsync())
            .TryGetValue(organizationId, out var organizationAbility);
        return organizationAbility;
    }
#nullable disable

    public virtual async Task<ConcurrentDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        await InitProviderAbilitiesAsync();
        return _providerAbilities;
    }

    public virtual async Task UpsertProviderAbilityAsync(Provider provider)
    {
        await InitProviderAbilitiesAsync();
        var newAbility = new ProviderAbility(provider);

        _providerAbilities[provider.Id] = newAbility;
    }

    public virtual async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        await InitOrganizationAbilitiesAsync();
        var newAbility = new OrganizationAbility(organization);

        _orgAbilities[organization.Id] = newAbility;
    }

    public virtual Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        _orgAbilities?.TryRemove(organizationId, out _);
        return Task.CompletedTask;
    }

    public virtual Task DeleteProviderAbilityAsync(Guid providerId)
    {
        _providerAbilities?.TryRemove(providerId, out _);
        return Task.CompletedTask;
    }

    private async Task InitOrganizationAbilitiesAsync()
    {
        var now = DateTime.UtcNow;

        if (_orgAbilities != null && (now - _lastOrgAbilityRefresh) <= _orgAbilitiesRefreshInterval)
        {
            return;
        }

        await _orgInitLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock to avoid redundant refresh
            now = DateTime.UtcNow;
            if (_orgAbilities == null || (now - _lastOrgAbilityRefresh) > _orgAbilitiesRefreshInterval)
            {
                var abilities = await _organizationRepository.GetManyAbilitiesAsync();
                _orgAbilities = new ConcurrentDictionary<Guid, OrganizationAbility>(
                    abilities.ToDictionary(a => a.Id));
                _lastOrgAbilityRefresh = now;
            }
        }
        finally
        {
            _orgInitLock.Release();
        }
    }

    private async Task InitProviderAbilitiesAsync()
    {
        var now = DateTime.UtcNow;

        if (_providerAbilities != null && (now - _lastProviderAbilityRefresh) <= _orgAbilitiesRefreshInterval)
        {
            return;
        }

        await _providerInitLock.WaitAsync();
        try
        {
            // Double-check after acquiring lock to avoid redundant refresh
            now = DateTime.UtcNow;
            if (_providerAbilities == null || (now - _lastProviderAbilityRefresh) > _orgAbilitiesRefreshInterval)
            {
                var abilities = await _providerRepository.GetManyAbilitiesAsync();
                _providerAbilities = new ConcurrentDictionary<Guid, ProviderAbility>(
                    abilities.ToDictionary(a => a.Id));
                _lastProviderAbilityRefresh = now;
            }
        }
        finally
        {
            _providerInitLock.Release();
        }
    }
}
