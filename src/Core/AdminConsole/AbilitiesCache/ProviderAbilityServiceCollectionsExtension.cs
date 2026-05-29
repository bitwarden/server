using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public static class ProviderAbilityServiceCollectionsExtension
{
    public static IServiceCollection AddProviderAbilityCache(this IServiceCollection services,
        GlobalSettings globalSettings) =>
        services.AddExtendedCache(ExtendedProviderAbilityCacheService.CacheName, globalSettings)
            .AddSingleton<IProviderAbilityCacheService, ExtendedProviderAbilityCacheService>();
}
