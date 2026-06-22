using Bit.Core.AdminConsole.Models.Data.Provider;

namespace Bit.Core.Services;

public interface IApplicationCacheService
{
    [Obsolete("We are transitioning to a new cache pattern. Please consult the Admin Console team before using.", false)]
    Task<IDictionary<Guid, ProviderAbility>> GetProviderAbilitiesAsync();
}
