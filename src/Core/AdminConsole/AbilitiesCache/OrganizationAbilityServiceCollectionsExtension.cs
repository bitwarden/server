using Bit.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.AdminConsole.AbilitiesCache;

public static class OrganizationAbilityServiceCollectionsExtension
{
    public static IServiceCollection AddOrganizationAbilityCache(this IServiceCollection serviceCollection,
        GlobalSettings globalSettings) =>
        serviceCollection.AddExtendedCache(ExtendedOrganizationAbilityCacheConstants.CacheName, globalSettings)
            .AddScoped<IOrganizationAbilityCacheService, ExtendedOrganizationAbilityCacheService>();
}
