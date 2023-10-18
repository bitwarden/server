using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Entities;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
}
