using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push;

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
        globalSettings?.NotificationHubPool?.NotificationHubs?.ForEach(hub =>
        {
            _logger.LogInformation("HubName: {HubName}, EnableSendTracing: {EnableSendTracing}, RegistrationStartDate: {RegistrationStartDate}, RegistrationEndDate: {RegistrationEndDate}", hub.HubName, hub.EnableSendTracing, hub.RegistrationStartDate, hub.RegistrationEndDate);
        });
    }

    public Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        PushToServices((s) => s.PushSyncCipherCreateAsync(cipher, collectionIds));
        return Task.FromResult(0);
    }

    public Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        PushToServices((s) => s.PushSyncCipherUpdateAsync(cipher, collectionIds));
        return Task.FromResult(0);
    }

    public Task PushSyncCipherDeleteAsync(Cipher cipher)
    {
        PushToServices((s) => s.PushSyncCipherDeleteAsync(cipher));
        return Task.FromResult(0);
    }

    public Task PushSyncFolderCreateAsync(Folder folder)
    {
        PushToServices((s) => s.PushSyncFolderCreateAsync(folder));
        return Task.FromResult(0);
    }

    public Task PushSyncFolderUpdateAsync(Folder folder)
    {
        PushToServices((s) => s.PushSyncFolderUpdateAsync(folder));
        return Task.FromResult(0);
    }

    public Task PushSyncFolderDeleteAsync(Folder folder)
    {
        PushToServices((s) => s.PushSyncFolderDeleteAsync(folder));
        return Task.FromResult(0);
    }

    public Task PushSyncCiphersAsync(Guid userId)
    {
        PushToServices((s) => s.PushSyncCiphersAsync(userId));
        return Task.FromResult(0);
    }

    public Task PushSyncVaultAsync(Guid userId)
    {
        PushToServices((s) => s.PushSyncVaultAsync(userId));
        return Task.FromResult(0);
    }

    public Task PushSyncOrganizationsAsync(Guid userId)
    {
        PushToServices((s) => s.PushSyncOrganizationsAsync(userId));
        return Task.FromResult(0);
    }

    public Task PushSyncOrgKeysAsync(Guid userId)
    {
        PushToServices((s) => s.PushSyncOrgKeysAsync(userId));
        return Task.FromResult(0);
    }

    public Task PushSyncSettingsAsync(Guid userId)
    {
        PushToServices((s) => s.PushSyncSettingsAsync(userId));
        return Task.FromResult(0);
    }

    public Task PushLogOutAsync(Guid userId, bool excludeCurrentContext = false)
    {
        PushToServices((s) => s.PushLogOutAsync(userId, excludeCurrentContext));
        return Task.FromResult(0);
    }

    public Task PushSyncSendCreateAsync(Send send)
    {
        PushToServices((s) => s.PushSyncSendCreateAsync(send));
        return Task.FromResult(0);
    }

    public Task PushSyncSendUpdateAsync(Send send)
    {
        PushToServices((s) => s.PushSyncSendUpdateAsync(send));
        return Task.FromResult(0);
    }

    public Task PushAuthRequestAsync(AuthRequest authRequest)
    {
        PushToServices((s) => s.PushAuthRequestAsync(authRequest));
        return Task.FromResult(0);
    }

    public Task PushAuthRequestResponseAsync(AuthRequest authRequest)
    {
        PushToServices((s) => s.PushAuthRequestResponseAsync(authRequest));
        return Task.FromResult(0);
    }

    public Task PushSyncSendDeleteAsync(Send send)
    {
        PushToServices((s) => s.PushSyncSendDeleteAsync(send));
        return Task.FromResult(0);
    }

    public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier,
        string deviceId = null)
    {
        PushToServices((s) => s.SendPayloadToUserAsync(userId, type, payload, identifier, deviceId));
        return Task.FromResult(0);
    }

    public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier,
        string deviceId = null)
    {
        PushToServices((s) => s.SendPayloadToOrganizationAsync(orgId, type, payload, identifier, deviceId));
        return Task.FromResult(0);
    }

    private void PushToServices(Func<IPushNotificationService, Task> pushFunc)
    {
        if (_services != null)
        {
            foreach (var service in _services)
            {
                pushFunc(service);
            }
        }
    }
}
