using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class InMemoryApplicationCacheService(
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    TimeProvider timeProvider)
    : IApplicationCacheService
{
    private ConcurrentDictionary<Guid, OrganizationAbility> _orgAbilities = new();
    private readonly SemaphoreSlim _orgInitLock = new(1, 1);
    private DateTimeOffset _lastOrgAbilityRefresh = DateTimeOffset.MinValue;

    private ConcurrentDictionary<Guid, ProviderAbility> _providerAbilities = new();
    private readonly SemaphoreSlim _providerInitLock = new(1, 1);
    private DateTimeOffset _lastProviderAbilityRefresh = DateTimeOffset.MinValue;

    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(10);

    public virtual async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        await InitOrganizationAbilitiesAsync();
        return _orgAbilities;
    }

    public async Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid organizationId)
    {
        (await GetOrganizationAbilitiesAsync())
            .TryGetValue(organizationId, out var organizationAbility);
        return organizationAbility;
    }

    public virtual async Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        await InitProviderAbilitiesAsync();
        return _providerAbilities;
    }

    public virtual async Task UpsertProviderAbilityAsync(Provider provider)
    {
        await InitProviderAbilitiesAsync();
        _providerAbilities.AddOrUpdate(
            provider.Id,
            _ => new ProviderAbility(provider),
            (_, _) => new ProviderAbility(provider));
    }

    public virtual async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        await InitOrganizationAbilitiesAsync();

        _orgAbilities.AddOrUpdate(
            organization.Id,
            _ => new OrganizationAbility(organization),
            (_, _) => new OrganizationAbility(organization));
    }

    public virtual Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        _orgAbilities.TryRemove(organizationId, out _);
        return Task.CompletedTask;
    }

    public virtual Task DeleteProviderAbilityAsync(Guid providerId)
    {
        _providerAbilities.TryRemove(providerId, out _);
        return Task.CompletedTask;
    }

    private async Task InitOrganizationAbilitiesAsync() =>
        await InitAbilitiesAsync<OrganizationAbility>(
            dict => _orgAbilities = dict,
            () => _lastOrgAbilityRefresh,
            dt => _lastOrgAbilityRefresh = dt,
            _orgInitLock,
            async () => await organizationRepository.GetManyAbilitiesAsync(),
            _refreshInterval,
            ability => ability.Id);

    private async Task InitProviderAbilitiesAsync() =>
       await InitAbilitiesAsync<ProviderAbility>(
            concurrentDictionary => _providerAbilities = concurrentDictionary,
            () => _lastProviderAbilityRefresh,
            dateTime => _lastProviderAbilityRefresh = dateTime,
            _providerInitLock,
            async () => await providerRepository.GetManyAbilitiesAsync(),
            _refreshInterval,
            ability => ability.Id);


    private async Task InitAbilitiesAsync<TAbility>(
        Action<ConcurrentDictionary<Guid, TAbility>> setCache,
        Func<DateTimeOffset> getLastRefresh,
        Action<DateTimeOffset> setLastRefresh,
        SemaphoreSlim @lock,
        Func<Task<IEnumerable<TAbility>>> fetchFunc,
        TimeSpan refreshInterval,
        Func<TAbility, Guid> getId)
    {
        if (SkipRefresh())
        {
            return;
        }

        await @lock.WaitAsync();
        try
        {
            if (SkipRefresh())
            {
                return;
            }

            var sources = await fetchFunc();
            var abilities = new ConcurrentDictionary<Guid, TAbility>(
                sources.ToDictionary(getId));
            setCache(abilities);
            setLastRefresh(timeProvider.GetUtcNow());
        }
        finally
        {
            @lock.Release();
        }

        bool SkipRefresh()
        {
            return timeProvider.GetUtcNow() - getLastRefresh() <= refreshInterval;
        }
    }

}
