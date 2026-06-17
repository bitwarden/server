using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
#nullable enable
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
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
