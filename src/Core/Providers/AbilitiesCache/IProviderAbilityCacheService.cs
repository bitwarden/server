using Bit.Core.Providers.Entities;
using Bit.Core.Providers.Models.Data;

namespace Bit.Core.Providers.AbilitiesCache;

public interface IProviderAbilityCacheService
{
    Task<ProviderAbility?> GetProviderAbilityAsync(Guid providerId, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync(IEnumerable<Guid> providerIds, CancellationToken cancellationToken = default);
    Task UpsertProviderAbilityAsync(Provider provider);
    Task DeleteProviderAbilityAsync(Guid providerId);
}
