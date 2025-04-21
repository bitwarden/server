#nullable enable
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push.Internal;

public class MultiServicePushNotificationService : IPushNotificationService
{
    private readonly IEnumerable<IPushNotificationService> _services;
    private readonly ILogger<MultiServicePushNotificationService> _logger;

    public MultiServicePushNotificationService(
        [FromKeyedServices("implementation")] IEnumerable<IPushNotificationService> services,
        ILogger<MultiServicePushNotificationService> logger,
        GlobalSettings globalSettings)
    {
        _services = services;

        _logger = logger;
        _logger.LogInformation("Hub services: {Services}", _services.Count());
        globalSettings.NotificationHubPool?.NotificationHubs?.ForEach(hub =>
        {
            _logger.LogInformation("HubName: {HubName}, EnableSendTracing: {EnableSendTracing}, RegistrationStartDate: {RegistrationStartDate}, RegistrationEndDate: {RegistrationEndDate}", hub.HubName, hub.EnableSendTracing, hub.RegistrationStartDate, hub.RegistrationEndDate);
        });
    }

    public Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        return PushToServices((s) => s.PushSyncCipherCreateAsync(cipher, collectionIds));
    }

    public Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        return PushToServices((s) => s.PushSyncCipherUpdateAsync(cipher, collectionIds));
    }

    public Task PushSyncCipherDeleteAsync(Cipher cipher)
    {
        return PushToServices((s) => s.PushSyncCipherDeleteAsync(cipher));
    }

    public Task PushSyncFolderCreateAsync(Folder folder)
    {
        return PushToServices((s) => s.PushSyncFolderCreateAsync(folder));
    }

    public Task PushSyncFolderUpdateAsync(Folder folder)
    {
        return PushToServices((s) => s.PushSyncFolderUpdateAsync(folder));
    }

    public Task PushSyncFolderDeleteAsync(Folder folder)
    {
        return PushToServices((s) => s.PushSyncFolderDeleteAsync(folder));
    }

    public Task PushSyncCiphersAsync(Guid userId)
    {
        return PushToServices((s) => s.PushSyncCiphersAsync(userId));
    }

    public Task PushSyncVaultAsync(Guid userId)
    {
        return PushToServices((s) => s.PushSyncVaultAsync(userId));
    }

    public Task PushSyncOrganizationsAsync(Guid userId)
    {
        return PushToServices((s) => s.PushSyncOrganizationsAsync(userId));
    }

    public Task PushSyncOrgKeysAsync(Guid userId)
    {
        return PushToServices((s) => s.PushSyncOrgKeysAsync(userId));
    }

    public Task PushSyncSettingsAsync(Guid userId)
    {
        return PushToServices((s) => s.PushSyncSettingsAsync(userId));
    }

    public Task PushLogOutAsync(Guid userId, bool excludeCurrentContext = false)
    {
        return PushToServices((s) => s.PushLogOutAsync(userId, excludeCurrentContext));
    }

    public Task PushSyncSendCreateAsync(Send send)
    {
        return PushToServices((s) => s.PushSyncSendCreateAsync(send));
    }

    public Task PushSyncSendUpdateAsync(Send send)
    {
        return PushToServices((s) => s.PushSyncSendUpdateAsync(send));
    }

    public Task PushAuthRequestAsync(AuthRequest authRequest)
    {
        return PushToServices((s) => s.PushAuthRequestAsync(authRequest));
    }

    public Task PushAuthRequestResponseAsync(AuthRequest authRequest)
    {
        return PushToServices((s) => s.PushAuthRequestResponseAsync(authRequest));
    }

    public Task PushSyncSendDeleteAsync(Send send)
    {
        return PushToServices((s) => s.PushSyncSendDeleteAsync(send));
    }

    public Task PushSyncOrganizationStatusAsync(Organization organization)
    {
        return PushToServices((s) => s.PushSyncOrganizationStatusAsync(organization));
    }

    public Task PushSyncOrganizationCollectionManagementSettingsAsync(Organization organization)
    {
        return PushToServices(s => s.PushSyncOrganizationCollectionManagementSettingsAsync(organization));
    }

    public Task PushNotificationAsync(Notification notification)
    {
        return PushToServices((s) => s.PushNotificationAsync(notification));
    }

    public Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        return PushToServices((s) => s.PushNotificationStatusAsync(notification, notificationStatus));
    }

    public Task SendPayloadToInstallationAsync(string installationId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        return PushToServices((s) =>
            s.SendPayloadToInstallationAsync(installationId, type, payload, identifier, deviceId, clientType));
    }

    public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        return PushToServices((s) => s.SendPayloadToUserAsync(userId, type, payload, identifier, deviceId, clientType));
    }

    public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        return PushToServices((s) => s.SendPayloadToOrganizationAsync(orgId, type, payload, identifier, deviceId, clientType));
    }

    public Task PushPendingSecurityTasksAsync(Guid userId)
    {
        return PushToServices((s) => s.PushPendingSecurityTasksAsync(userId));
    }

    private Task PushToServices(Func<IPushNotificationService, Task> pushFunc)
    {
        if (!_services.Any())
        {
            _logger.LogWarning("No services found to push notification");
            return Task.CompletedTask;
        }


        #if DEBUG
        var tasks = new List<Task>();
        #endif

        foreach (var service in _services)
        {
            _logger.LogDebug("Pushing notification to service {ServiceName}", service.GetType().Name);
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
}
