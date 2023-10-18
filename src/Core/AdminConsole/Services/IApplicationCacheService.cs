using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Organizations;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.AdminConsole.Services;

public interface IApplicationCacheService
{
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
}
