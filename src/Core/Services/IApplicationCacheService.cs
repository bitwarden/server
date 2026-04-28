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
    /// <summary>
    /// Gets the cached <see cref="ProviderAbility"/> for the specified provider.
    /// </summary>
    /// <param name="providerId">The ID of the provider.</param>
    /// <returns>The <see cref="ProviderAbility"/> if found; otherwise, <c>null</c>.</returns>
    Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId);
#nullable disable
    [Obsolete("We are transitioning to a new cache pattern. Please consult the Admin Console team before using.", false)]
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
    /// <summary>
    /// Gets cached <see cref="ProviderAbility"/> entries for the specified providers.
    /// Provider IDs not found in the cache are silently excluded from the result.
    /// </summary>
    /// <param name="providerIds">The IDs of the providers to look up.</param>
    /// <returns>A dictionary mapping each found provider ID to its <see cref="ProviderAbility"/>.</returns>
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds);
    /// <summary>
    /// Gets cached <see cref="OrganizationAbility"/> entries for the specified organizations.
    /// Organization IDs not found in the cache are silently excluded from the result.
    /// </summary>
    /// <param name="orgIds">The IDs of the organizations to look up.</param>
    /// <returns>A dictionary mapping each found organization ID to its <see cref="OrganizationAbility"/>.</returns>
    Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<Guid> orgIds);
    Task UpsertOrganizationAbilityAsync(Organization organization);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteOrganizationAbilityAsync(Guid organizationId);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
