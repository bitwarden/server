using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

public class MultiServicePushNotificationService : IPushNotificationService
{
    private readonly IPushEngine[] _services;
    private readonly ICollectionCipherRepository _collectionCipherRepository;

    public Guid InstallationId { get; }

    public TimeProvider TimeProvider { get; }

    public ILogger Logger { get; }

    public MultiServicePushNotificationService(
        IEnumerable<IPushEngine> services,
        ICollectionCipherRepository collectionCipherRepository,
        ILogger<MultiServicePushNotificationService> logger,
        GlobalSettings globalSettings,
        TimeProvider timeProvider)
    {
        // Filter out any NoopPushEngine's
        _services = [.. services.Where(engine => engine is not NoopPushEngine)];
        _collectionCipherRepository = collectionCipherRepository;

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

    public async Task PushCipherAsync(Cipher cipher, PushType pushType, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            var collectionIdList = collectionIds?.Distinct().ToList() ?? [];
            if (collectionIdList.Count == 0)
            {
                collectionIdList = await ResolveCollectionIdsAsync(cipher);
                if (collectionIdList.Count == 0)
                {
                    Logger.LogWarning(
                        "Skipping push notification for organization cipher {CipherId} in organization {OrganizationId} because no collection IDs were provided or found.",
                        cipher.Id,
                        cipher.OrganizationId.Value);
                    return;
                }
            }

            var userIds = await _collectionCipherRepository.GetUserIdsByCollectionIdsAsync(collectionIdList);
            var pushTasks = userIds.Select(userId =>
            {
                var message = new SyncCipherPushNotification
                {
                    Id = cipher.Id,
                    UserId = userId,
                    OrganizationId = cipher.OrganizationId,
                    RevisionDate = cipher.RevisionDate,
                    CollectionIds = collectionIdList,
                };

                return PushToServices(s => s.PushAsync(new PushNotification<SyncCipherPushNotification>
                {
                    Type = pushType,
                    Target = NotificationTarget.User,
                    TargetId = userId,
                    Payload = message,
                    ExcludeCurrentContext = true,
                }));
            });

            await Task.WhenAll(pushTasks);

            return;
        }

        await PushToServices(s => s.PushCipherAsync(cipher, pushType, collectionIds));
    }

    private async Task<List<Guid>> ResolveCollectionIdsAsync(Cipher cipher)
    {
        if (!cipher.OrganizationId.HasValue)
        {
            return [];
        }

        var collectionIds = await _collectionCipherRepository
            .GetCollectionIdsByCipherIdAsync(cipher.Id);

        return [.. collectionIds];
    }

    public Task PushAsync<T>(PushNotification<T> pushNotification) where T : class
    {
        return PushToServices((s) => s.PushAsync(pushNotification));
    }
}
