using Bit.Core.AdminConsole.Entities;
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
    /// <summary>
    /// Gets cached <see cref="OrganizationAbility"/> entries for the specified organizations.
    /// Organization IDs not found in the cache are silently excluded from the result.
    /// </summary>
    /// <param name="orgIds">The IDs of the organizations to look up.</param>
    /// <returns>A dictionary mapping each found organization ID to its <see cref="OrganizationAbility"/>.</returns>
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds);
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
}
