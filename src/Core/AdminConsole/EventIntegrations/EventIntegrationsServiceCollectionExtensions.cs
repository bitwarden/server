using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

public static class EventIntegrationsServiceCollectionExtensions
{
    /// <summary>
    /// Adds all event integrations commands, queries, and required cache infrastructure.
    /// This method is idempotent and can be called multiple times safely.
    /// </summary>
    public static IServiceCollection AddEventIntegrationsCommandsQueries(
        this IServiceCollection services,
        GlobalSettings globalSettings)
    {
        // Ensure cache is registered first - commands depend on this keyed cache.
        // This is idempotent for the same named cache, so it's safe to call.
        services.AddExtendedCache(EventIntegrationsCacheConstants.CacheName, globalSettings);

        // Add all commands/queries
        services.AddOrganizationIntegrationCommandsQueries();

        return services;
    }

    internal static IServiceCollection AddOrganizationIntegrationCommandsQueries(this IServiceCollection services)
    {
        services.TryAddScoped<ICreateOrganizationIntegrationCommand, CreateOrganizationIntegrationCommand>();
        services.TryAddScoped<IUpdateOrganizationIntegrationCommand, UpdateOrganizationIntegrationCommand>();
        services.TryAddScoped<IDeleteOrganizationIntegrationCommand, DeleteOrganizationIntegrationCommand>();
        services.TryAddScoped<IGetOrganizationIntegrationsQuery, GetOrganizationIntegrationsQuery>();

        return services;
    }
}
