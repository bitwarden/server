using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Repositories;

namespace Bit.Core.Services
{
    public class InMemoryApplicationCacheService : IApplicationCacheService
    {
        private readonly IOrganizationRepository _organizationRepository;
        private DateTime _lastOrgAbilityRefresh = DateTime.MinValue;
        private IDictionary<Guid, OrganizationAbility> _orgAbilities;
        private TimeSpan _orgAbilitiesRefreshInterval = TimeSpan.FromMinutes(10);

        public InMemoryApplicationCacheService(
            IOrganizationRepository organizationRepository)
        {
            _organizationRepository = organizationRepository;
        }

        public virtual async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync()
        {
            await InitOrganizationAbilitiesAsync();
            return _orgAbilities;
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
    }
}
