using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface IApplicationCacheService
    {
        Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();
        Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
        Task UpsertOrganizationAbilityAsync(Organization organization);
        Task DeleteOrganizationAbilityAsync(Guid organizationId);
    }
}
