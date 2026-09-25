using System.Diagnostics;
using Bit.Core.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Bit.SharedWeb.Utilities;

public static class GlobalSettingsServiceCollectionExtensions
{
    private static GlobalSettings CreateGlobalSettings(IConfiguration configuration, IHostEnvironment environment)
    {
        var globalSettings = new GlobalSettings();
        ConfigurationBinder.Bind(configuration.GetSection("GlobalSettings"), globalSettings);

        if (environment.IsDevelopment() && configuration.GetValue<bool>("developSelfHosted"))
        {
            // Override settings with selfHostedOverride settings
            ConfigurationBinder.Bind(configuration.GetSection("Dev:SelfHostOverride:GlobalSettings"), globalSettings);
        }

        return globalSettings;
    }

    /// <summary>
    /// A convenience method to ensure <see cref="GlobalSettings"/> and <see cref="IGlobalSettings"/>
    /// are added to the <paramref name="services"/> but does not require any up front dependencies.
    /// </summary>
    /// <param name="services">The services to add to.</param>
    /// <returns>The service collection for additional chaining.</returns>
    public static IServiceCollection AddGlobalSettings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var environment = sp.GetRequiredService<IHostEnvironment>();

            return CreateGlobalSettings(config, environment);
        });

        services.TryAddSingleton<IGlobalSettings>(sp => sp.GetRequiredService<GlobalSettings>());

        return services;
    }

    /// <summary>
    /// Ensures that <see cref="GlobalSettings"/> and <see cref="IGlobalSettings"/> are registered
    /// as services in the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// If <see cref="GlobalSettings"/> has already been registered as an instance
    /// in the <see cref="IServiceCollection"/> then binding with the configuration passed in
    /// it is <b>not</b> done and the existing instance is used instead.
    /// </remarks>
    /// <param name="services">The collection to add the services to.</param>
    /// <param name="configuration"></param>
    /// <param name="environment"></param>
    /// <returns>The newly binded <see cref="GlobalSettings"/> instance or the existing instance stored in the services.</returns>
    public static GlobalSettings AddGlobalSettingsServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var globalSettingsService = services.SingleOrDefault(
            sd => sd.ServiceType == typeof(GlobalSettings)
        );

        // If we've already created an instance service, re-use that and avoid re-binding
        if (globalSettingsService is not null
            && globalSettingsService.ImplementationInstance is not null)
        {

            Debug.Assert(services.Any(sd => sd.ServiceType == typeof(IGlobalSettings)));
            return (GlobalSettings)globalSettingsService.ImplementationInstance;
        }

        if (globalSettingsService is not null)
        {
            // If it has been added before but not as an instance, remove it
            // so we can add it again as an instance
            services.Remove(globalSettingsService);
        }

        var globalSettings = CreateGlobalSettings(configuration, environment);

        // No try needed because we validated that no service of this
        // type exists or we just removed it.
        services.AddSingleton(globalSettings);
        services.TryAddSingleton<IGlobalSettings>(sp => sp.GetRequiredService<GlobalSettings>());

        return globalSettings;
    }
}
