using Bit.Core.AdminConsole.AbilitiesCache;
using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.Services.Implementations;

public class FeatureRoutedCacheService(
    IVCurrentInMemoryApplicationCacheService inMemoryApplicationCacheService)
    : IApplicationCacheService
{

    public Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync() =>
        inMemoryApplicationCacheService.GetProviderAbilitiesAsync();

}
