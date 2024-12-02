using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.NotificationHub;

public class NotificationHubPushNotificationService : IPushNotificationService
{
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _enableTracing = false;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly ILogger _logger;

    public NotificationHubPushNotificationService(
        IInstallationDeviceRepository installationDeviceRepository,
        INotificationHubPool notificationHubPool,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationsApiPushNotificationService> logger)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _httpContextAccessor = httpContextAccessor;
        _notificationHubPool = notificationHubPool;
        _logger = logger;
    }

    public async Task PushSyncCipherCreateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        await PushCipherAsync(cipher, PushType.SyncCipherCreate, collectionIds);
    }

    public async Task PushSyncCipherUpdateAsync(Cipher cipher, IEnumerable<Guid> collectionIds)
    {
        await PushCipherAsync(cipher, PushType.SyncCipherUpdate, collectionIds);
    }

    public async Task PushSyncCipherDeleteAsync(Cipher cipher)
    {
        await PushCipherAsync(cipher, PushType.SyncLoginDelete, null);
    }

    private async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid> collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            // We cannot send org pushes since access logic is much more complicated than just the fact that they belong
            // to the organization. Potentially we could blindly send to just users that have the access all permission
            // device registration needs to be more granular to handle that appropriately. A more brute force approach could
            // me to send "full sync" push to all org users, but that has the potential to DDOS the API in bursts.

            // await SendPayloadToOrganizationAsync(cipher.OrganizationId.Value, type, message, true);
        }
        else if (cipher.UserId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            };

            await SendPayloadToUserAsync(cipher.UserId.Value, type, message, true);
        }
    }

    public async Task PushSyncFolderCreateAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderCreate);
    }

    public async Task PushSyncFolderUpdateAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderUpdate);
    }

    public async Task PushSyncFolderDeleteAsync(Folder folder)
    {
        await PushFolderAsync(folder, PushType.SyncFolderDelete);
    }

    private async Task PushFolderAsync(Folder folder, PushType type)
    {
        var message = new SyncFolderPushNotification
        {
            Id = folder.Id,
            UserId = folder.UserId,
            RevisionDate = folder.RevisionDate
        };

        await SendPayloadToUserAsync(folder.UserId, type, message, true);
    }

    public async Task PushSyncCiphersAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncCiphers);
    }

    public async Task PushSyncVaultAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncVault);
    }

    public async Task PushSyncOrganizationsAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncOrganizations);
    }

    public async Task PushSyncOrgKeysAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncOrgKeys);
    }

    public async Task PushSyncSettingsAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.SyncSettings);
    }

    public async Task PushLogOutAsync(Guid userId, bool excludeCurrentContext = false)
    {
        await PushUserAsync(userId, PushType.LogOut, excludeCurrentContext);
    }

    private async Task PushUserAsync(Guid userId, PushType type, bool excludeCurrentContext = false)
    {
        var message = new UserPushNotification
        {
            UserId = userId,
            Date = DateTime.UtcNow
        };

        await SendPayloadToUserAsync(userId, type, message, excludeCurrentContext);
    }

    public async Task PushSyncSendCreateAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendCreate);
    }

    public async Task PushSyncSendUpdateAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendUpdate);
    }

    public async Task PushSyncSendDeleteAsync(Send send)
    {
        await PushSendAsync(send, PushType.SyncSendDelete);
    }

    private async Task PushSendAsync(Send send, PushType type)
    {
        if (send.UserId.HasValue)
        {
            var message = new SyncSendPushNotification
            {
                Id = send.Id,
                UserId = send.UserId.Value,
                RevisionDate = send.RevisionDate
            };

            await SendPayloadToUserAsync(message.UserId, type, message, true);
        }
    }

    public async Task PushAuthRequestAsync(AuthRequest authRequest)
    {
        await PushAuthRequestAsync(authRequest, PushType.AuthRequest);
    }

    public async Task PushAuthRequestResponseAsync(AuthRequest authRequest)
    {
        await PushAuthRequestAsync(authRequest, PushType.AuthRequestResponse);
    }

    private async Task PushAuthRequestAsync(AuthRequest authRequest, PushType type)
    {
        var message = new AuthRequestPushNotification
        {
            Id = authRequest.Id,
            UserId = authRequest.UserId
        };

        await SendPayloadToUserAsync(authRequest.UserId, type, message, true);
    }

    private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload, bool excludeCurrentContext)
    {
        await SendPayloadToUserAsync(userId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
    }

    private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload, bool excludeCurrentContext)
    {
        await SendPayloadToUserAsync(orgId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext));
    }

    public async Task SendPayloadToUserAsync(string userId, PushType type, object payload, string identifier,
        string deviceId = null)
    {
        var tag = BuildTag($"template:payload_userId:{SanitizeTagInput(userId)}", identifier);
        await SendPayloadAsync(tag, type, payload);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    public async Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string identifier,
        string deviceId = null)
    {
        var tag = BuildTag($"template:payload && organizationId:{SanitizeTagInput(orgId)}", identifier);
        await SendPayloadAsync(tag, type, payload);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    private string GetContextIdentifier(bool excludeCurrentContext)
    {
        if (!excludeCurrentContext)
        {
            return null;
        }

        var currentContext = _httpContextAccessor?.HttpContext?.
            RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        return currentContext?.DeviceIdentifier;
    }

    private string BuildTag(string tag, string identifier)
    {
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            tag += $" && !deviceIdentifier:{SanitizeTagInput(identifier)}";
        }

        return $"({tag})";
    }

    private async Task SendPayloadAsync(string tag, PushType type, object payload)
    {
        var results = await _notificationHubPool.AllClients.SendTemplateNotificationAsync(
            new Dictionary<string, string>
            {
                { "type",  ((byte)type).ToString() },
                { "payload", JsonSerializer.Serialize(payload) }
            }, tag);

        if (_enableTracing)
        {
            foreach (var (client, outcome) in results)
            {
                if (!client.EnableTestSend)
                {
                    continue;
                }
                _logger.LogInformation("Azure Notification Hub Tracking ID: {Id} | {Type} push notification with {Success} successes and {Failure} failures with a payload of {@Payload} and result of {@Results}",
                    outcome.TrackingId, type, outcome.Success, outcome.Failure, payload, outcome.Results);
            }
        }
    }

    private string SanitizeTagInput(string input)
    {
        // Only allow a-z, A-Z, 0-9, and special characters -_:
        return Regex.Replace(input, "[^a-zA-Z0-9-_:]", string.Empty);
    }
}
