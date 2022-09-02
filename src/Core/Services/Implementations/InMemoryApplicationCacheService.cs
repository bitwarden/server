using Bit.Core.Entities;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class InMemoryApplicationCacheService : IApplicationCacheService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IProviderRepository _providerRepository;
    private DateTime _lastOrgAbilityRefresh = DateTime.MinValue;
    private IDictionary<Guid, OrganizationAbility> _orgAbilities;
    private TimeSpan _orgAbilitiesRefreshInterval = TimeSpan.FromMinutes(10);

    private IDictionary<Guid, ProviderAbility> _providerAbilities;

    public InMemoryApplicationCacheService(
        IOrganizationRepository organizationRepository, IProviderRepository providerRepository)
    {
        _organizationRepository = organizationRepository;
        _providerRepository = providerRepository;
    }

    public virtual async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
    {
        await InitOrganizationAbilitiesAsync();
        return _orgAbilities;
    }

    public virtual async Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync()
    {
        await InitProviderAbilitiesAsync();
        return _providerAbilities;
    }

    public virtual async Task UpsertProviderAbilityAsync(Provider provider)
    {
        await InitProviderAbilitiesAsync();
        var newAbility = new ProviderAbility(provider);

        if (_providerAbilities.ContainsKey(provider.Id))
        {
            _providerAbilities[provider.Id] = newAbility;
        }
        else
        {
            _providerAbilities.Add(provider.Id, newAbility);
        }
    }

    public virtual async Task UpsertOrganizationAbilityAsync(Organization organization)
    {
        await InitOrganizationAbilitiesAsync();
        var newAbility = new OrganizationAbility(organization);

        if (_orgAbilities.ContainsKey(organization.Id))
        {
            _orgAbilities[organization.Id] = newAbility;
        }
        else
        {
            _orgAbilities.Add(organization.Id, newAbility);
        }
    }

    public virtual Task DeleteOrganizationAbilityAsync(Guid organizationId)
    {
        if (_orgAbilities != null && _orgAbilities.ContainsKey(organizationId))
        {
            _orgAbilities.Remove(organizationId);
        }

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
