using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public interface IProviderAbilityCacheService
{
    Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds, CancellationToken cancellationToken = default);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
