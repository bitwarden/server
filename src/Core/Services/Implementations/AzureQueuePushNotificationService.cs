#nullable enable
using System.Text.Json;
using Azure.Storage.Queues;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Utilities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Core.Services;

public class AzureQueuePushNotificationService : IPushNotificationService
{
    private readonly QueueClient _queueClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IGlobalSettings _globalSettings;

    public AzureQueuePushNotificationService(
        [FromKeyedServices("notifications")] QueueClient queueClient,
        IHttpContextAccessor httpContextAccessor,
        IGlobalSettings globalSettings)
    {
        _queueClient = queueClient;
        _httpContextAccessor = httpContextAccessor;
        _globalSettings = globalSettings;
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

    private async Task PushCipherAsync(Cipher cipher, PushType type, IEnumerable<Guid>? collectionIds)
    {
        if (cipher.OrganizationId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                OrganizationId = cipher.OrganizationId,
                RevisionDate = cipher.RevisionDate,
                CollectionIds = collectionIds,
            };

            await SendMessageAsync(type, message, true);
        }
        else if (cipher.UserId.HasValue)
        {
            var message = new SyncCipherPushNotification
            {
                Id = cipher.Id,
                UserId = cipher.UserId,
                RevisionDate = cipher.RevisionDate,
            };

            await SendMessageAsync(type, message, true);
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

        await SendMessageAsync(type, message, true);
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
        var message = new UserPushNotification { UserId = userId, Date = DateTime.UtcNow };

        await SendMessageAsync(type, message, excludeCurrentContext);
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
        var message = new AuthRequestPushNotification { Id = authRequest.Id, UserId = authRequest.UserId };

        await SendMessageAsync(type, message, true);
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

    public async Task PushNotificationAsync(Notification notification)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = notification.Global ? _globalSettings.Installation.Id : null,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate
        };

        await SendMessageAsync(PushType.Notification, message, true);
    }

    public async Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = notification.Global ? _globalSettings.Installation.Id : null,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate
        };

        await SendMessageAsync(PushType.NotificationStatus, message, true);
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

            await SendMessageAsync(type, message, true);
        }
    }

    private async Task SendMessageAsync<T>(PushType type, T payload, bool excludeCurrentContext)
    {
        var contextId = GetContextIdentifier(excludeCurrentContext);
        var message = JsonSerializer.Serialize(new PushNotificationData<T>(type, payload, contextId),
            JsonHelpers.IgnoreWritingNull);
        await _queueClient.SendMessageAsync(message);
    }

    private string? GetContextIdentifier(bool excludeCurrentContext)
    {
        if (!excludeCurrentContext)
        {
            return null;
        }

        var currentContext =
            _httpContextAccessor?.HttpContext?.RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        return currentContext?.DeviceIdentifier;
    }

    public Task SendPayloadToInstallationAsync(string installationId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null) =>
        // Noop
        Task.CompletedTask;

    public Task SendPayloadToUserAsync(string userId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        // Noop
        return Task.FromResult(0);
    }

    public Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        // Noop
        return Task.FromResult(0);
    }

    public async Task PushSyncOrganizationStatusAsync(Organization organization)
    {
        var message = new OrganizationStatusPushNotification
        {
            OrganizationId = organization.Id,
            Enabled = organization.Enabled
        };
        await SendMessageAsync(PushType.SyncOrganizationStatusChanged, message, false);
    }

}
