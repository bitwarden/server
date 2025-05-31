#nullable enable
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

public class MultiServicePushNotificationService : IPushNotificationService
{
    private readonly IEnumerable<IPushEngine> _services;

    public Guid InstallationId { get; }

    public TimeProvider TimeProvider { get; }

    public ILogger Logger { get; }

    public MultiServicePushNotificationService(
        IEnumerable<IPushEngine> services,
        ILogger<MultiServicePushNotificationService> logger,
        GlobalSettings globalSettings,
        TimeProvider timeProvider)
    {
        _services = services;

        Logger = logger;
        Logger.LogInformation("Hub services: {Services}", _services.Count());
        globalSettings.NotificationHubPool?.NotificationHubs?.ForEach(hub =>
        {
            Logger.LogInformation("HubName: {HubName}, EnableSendTracing: {EnableSendTracing}, RegistrationStartDate: {RegistrationStartDate}, RegistrationEndDate: {RegistrationEndDate}", hub.HubName, hub.EnableSendTracing, hub.RegistrationStartDate, hub.RegistrationEndDate);
        });
        InstallationId = globalSettings.Installation.Id;
        TimeProvider = timeProvider;
    }

    private Task PushToServices(Func<IPushEngine, Task> pushFunc)
    {
        if (!_services.Any())
        {
            Logger.LogWarning("No services found to push notification");
            return Task.CompletedTask;
        }


#if DEBUG
        var tasks = new List<Task>();
#endif

        foreach (var service in _services)
        {
            Logger.LogDebug("Pushing notification to service {ServiceName}", service.GetType().Name);
#if DEBUG
            var task =
#endif
            pushFunc(service);
#if DEBUG
            tasks.Add(task);
#endif
        }

#if DEBUG
        return Task.WhenAll(tasks);
#else
        return Task.CompletedTask;
#endif
    }

    public Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds)
    {
        return PushToServices((s) => s.PushCipherAsync(cipher, pushType, collectionIds));
    }
    public Task PushAsync<T>(PushNotification<T> pushNotification) where T : class
    {
        return PushToServices((s) => s.PushAsync(pushNotification));
    }
}
