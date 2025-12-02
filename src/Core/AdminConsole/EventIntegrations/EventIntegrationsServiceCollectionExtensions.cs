using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations;
using Bit.Core.AdminConsole.EventIntegrations.OrganizationIntegrationConfigurations.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationIntegrations.Interfaces;
using Bit.Core.AdminConsole.Services;
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

        // Add Validator
        services.TryAddSingleton<IOrganizationIntegrationConfigurationValidator, OrganizationIntegrationConfigurationValidator>();

        // Add all commands/queries
        services.AddOrganizationIntegrationCommandsQueries();
        services.AddOrganizationIntegrationConfigurationCommandsQueries();

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

    internal static IServiceCollection AddOrganizationIntegrationConfigurationCommandsQueries(this IServiceCollection services)
    {
        services.TryAddScoped<ICreateOrganizationIntegrationConfigurationCommand, CreateOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IUpdateOrganizationIntegrationConfigurationCommand, UpdateOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IDeleteOrganizationIntegrationConfigurationCommand, DeleteOrganizationIntegrationConfigurationCommand>();
        services.TryAddScoped<IGetOrganizationIntegrationConfigurationsQuery, GetOrganizationIntegrationConfigurationsQuery>();

        return services;
    }
}
