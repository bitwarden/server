using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Platform.PushRegistration.Internal;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding the Push Registration feature.
/// </summary>
public static class PushRegistrationServiceCollectionExtensions
{
    /// <summary>
    /// Adds a <see cref="IPushRegistrationService"/> to the service collection.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddPushRegistration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // TODO: Should add feature that brings in IInstallationDeviceRepository once that is featurized

        // Register all possible variants under there concrete type.
        services.TryAddSingleton<RelayPushRegistrationService>();
        services.TryAddSingleton<NoopPushRegistrationService>();

        services.AddHttpClient();
        services.TryAddSingleton<INotificationHubPool, NotificationHubPool>();
        services.TryAddSingleton<NotificationHubPushRegistrationService>();

        services.TryAddSingleton<IPushRegistrationService>(static sp =>
        {
            var globalSettings = sp.GetRequiredService<GlobalSettings>();

            if (globalSettings.SelfHosted)
            {
                if (CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                    CoreHelpers.SettingHasValue(globalSettings.Installation.Key))
                {
                    return sp.GetRequiredService<RelayPushRegistrationService>();
                }

                return sp.GetRequiredService<NoopPushRegistrationService>();
            }

            return sp.GetRequiredService<NotificationHubPushRegistrationService>();
        });

        return services;
    }
}
