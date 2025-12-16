using Bit.Core.Platform.Push;
using Bit.Core.Platform.Push.Internal;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for adding the Push feature.
/// </summary>
public static class PushServiceCollectionExtensions
{
    /// <summary>
    /// Adds a <see cref="IPushNotificationService"/> to the services that can be used to send push notifications to
    /// end user devices. This method is safe to be ran multiple time provided <see cref="GlobalSettings"/> does not
    /// change between calls.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="globalSettings">The <see cref="GlobalSettings"/> to use to configure services.</param>
    /// <returns>The <see cref="IServiceCollection"/> for additional chaining.</returns>
    public static IServiceCollection AddPush(this IServiceCollection services, GlobalSettings globalSettings)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(globalSettings);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IPushNotificationService, MultiServicePushNotificationService>();

        if (globalSettings.SelfHosted)
        {
            if (globalSettings.Installation.Id == Guid.Empty)
            {
                throw new InvalidOperationException("Installation Id must be set for self-hosted installations.");
            }

            if (CoreHelpers.SettingHasValue(globalSettings.PushRelayBaseUri) &&
                CoreHelpers.SettingHasValue(globalSettings.Installation.Key))
            {
                // TODO: We should really define the HttpClient we will use here
                services.AddHttpClient();
                services.AddHttpContextAccessor();
                // We also depend on IDeviceRepository but don't explicitly add it right now.
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, RelayPushEngine>());
            }

            if (CoreHelpers.SettingHasValue(globalSettings.InternalIdentityKey) &&
                CoreHelpers.SettingHasValue(globalSettings.BaseServiceUri.InternalNotifications))
            {
                // TODO: We should really define the HttpClient we will use here
                services.AddHttpClient();
                services.AddHttpContextAccessor();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, NotificationsApiPushEngine>());
            }
        }
        else
        {
            services.TryAddSingleton<INotificationHubPool, NotificationHubPool>();
            services.AddHttpContextAccessor();

            // We also depend on IInstallationDeviceRepository but don't explicitly add it right now.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, NotificationHubPushEngine>());

            services.TryAddSingleton<IPushRelayer, NotificationHubPushEngine>();

            // if (CoreHelpers.SettingHasValue(globalSettings.Notifications?.ConnectionString))
            // {
            //     services.TryAddKeyedSingleton("notifications", static (sp, _) =>
            //     {
            //         var gs = sp.GetRequiredService<GlobalSettings>();
            //         return new QueueClient(gs.Notifications.ConnectionString, "notifications");
            //     });
            //
            //     // We not IHttpContextAccessor will be added above, no need to do it here.
            //     services.TryAddEnumerable(ServiceDescriptor.Singleton<IPushEngine, AzureQueuePushEngine>());
            // }
        }

        return services;
    }
}
