using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();

    // DO NOT USE. This is just a fake temporary function for the spike.
    // Calling code that uses this should use the new GET multiple resources endpoint when it’s ready.
    // https://bitwarden.atlassian.net/wiki/spaces/EN/pages/2465824806/ACTD+Research+organization+and+provider+ability+new+caching+implementation#GET-Multiple-organizations
    Task<IDictionary<Guid, OrganizationAbility>> FakeGetOrganizationAbilitiesAsync();

    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds);
#nullable enable
    Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId);
    Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId);
#nullable disable

    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds);
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
