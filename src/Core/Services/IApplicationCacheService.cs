using System.Collections.Concurrent;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.Models.Data.Organizations;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
    Task<ConcurrentDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync();
#nullable enable
    Task<OrganizationAbility?> GetOrganizationAbilityAsync(Guid orgId);
#nullable disable
    Task<ConcurrentDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
