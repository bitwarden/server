using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
    [Obsolete("We are transitioning to a new cache pattern. Please consult the Admin Console team before using.", false)]
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();
#nullable enable
    Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId);
#nullable disable
    [Obsolete("We are transitioning to a new cache pattern. Please consult the Admin Console team before using.", false)]
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
#nullable enable
    Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId);
#nullable disable
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds);
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds);
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
