﻿#nullable enable
using System.Text.Json;
using System.Text.RegularExpressions;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Notification = Bit.Core.NotificationCenter.Entities.Notification;

namespace Bit.Core.NotificationHub;

/// <summary>
/// Sends mobile push notifications to the Azure Notification Hub.
/// Used by Cloud-Hosted environments.
/// Received by Firebase for Android or APNS for iOS.
/// </summary>
public class NotificationHubPushNotificationService : IPushNotificationService
{
    private readonly IInstallationDeviceRepository _installationDeviceRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly bool _enableTracing = false;
    private readonly INotificationHubPool _notificationHubPool;
    private readonly ILogger _logger;
    private readonly IGlobalSettings _globalSettings;

    public NotificationHubPushNotificationService(
        IInstallationDeviceRepository installationDeviceRepository,
        INotificationHubPool notificationHubPool,
        IHttpContextAccessor httpContextAccessor,
        ILogger<NotificationHubPushNotificationService> logger,
        IGlobalSettings globalSettings)
    {
        _installationDeviceRepository = installationDeviceRepository;
        _httpContextAccessor = httpContextAccessor;
        _notificationHubPool = notificationHubPool;
        _logger = logger;
        _globalSettings = globalSettings;

        if (globalSettings.Installation.Id == Guid.Empty)
        {
            logger.LogWarning("Installation ID is not set. Push notifications for installations will not work.");
        }
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
        var message = new UserPushNotification { UserId = userId, Date = DateTime.UtcNow };

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

    public async Task PushNotificationAsync(Notification notification)
    {
        Guid? installationId = notification.Global && _globalSettings.Installation.Id != Guid.Empty
            ? _globalSettings.Installation.Id
            : null;

        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = installationId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate
        };

        if (notification.Global)
        {
            if (installationId.HasValue)
            {
                await SendPayloadToInstallationAsync(installationId.Value, PushType.Notification, message, true,
                    notification.ClientType);
            }
            else
            {
                _logger.LogWarning(
                    "Invalid global notification id {NotificationId} push notification. No installation id provided.",
                    notification.Id);
            }
        }
        else if (notification.UserId.HasValue)
        {
            await SendPayloadToUserAsync(notification.UserId.Value, PushType.Notification, message, true,
                notification.ClientType);
        }
        else if (notification.OrganizationId.HasValue)
        {
            await SendPayloadToOrganizationAsync(notification.OrganizationId.Value, PushType.Notification, message,
                true, notification.ClientType);
        }
        else
        {
            _logger.LogWarning("Invalid notification id {NotificationId} push notification", notification.Id);
        }
    }

    public async Task PushNotificationStatusAsync(Notification notification, NotificationStatus notificationStatus)
    {
        Guid? installationId = notification.Global && _globalSettings.Installation.Id != Guid.Empty
            ? _globalSettings.Installation.Id
            : null;

        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
            InstallationId = installationId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate
        };

        if (notification.Global)
        {
            if (installationId.HasValue)
            {
                await SendPayloadToInstallationAsync(installationId.Value, PushType.NotificationStatus, message, true,
                    notification.ClientType);
            }
            else
            {
                _logger.LogWarning(
                    "Invalid global notification status id {NotificationId} push notification. No installation id provided.",
                    notification.Id);
            }
        }
        else if (notification.UserId.HasValue)
        {
            await SendPayloadToUserAsync(notification.UserId.Value, PushType.NotificationStatus, message, true,
                notification.ClientType);
        }
        else if (notification.OrganizationId.HasValue)
        {
            await SendPayloadToOrganizationAsync(notification.OrganizationId.Value, PushType.NotificationStatus,
                message, true, notification.ClientType);
        }
        else
        {
            _logger.LogWarning("Invalid notification status id {NotificationId} push notification", notification.Id);
        }
    }

    private async Task PushAuthRequestAsync(AuthRequest authRequest, PushType type)
    {
        var message = new AuthRequestPushNotification { Id = authRequest.Id, UserId = authRequest.UserId };

        await SendPayloadToUserAsync(authRequest.UserId, type, message, true);
    }

    private async Task SendPayloadToInstallationAsync(Guid installationId, PushType type, object payload,
        bool excludeCurrentContext, ClientType? clientType = null)
    {
        await SendPayloadToInstallationAsync(installationId.ToString(), type, payload,
            GetContextIdentifier(excludeCurrentContext), clientType: clientType);
    }

    private async Task SendPayloadToUserAsync(Guid userId, PushType type, object payload, bool excludeCurrentContext,
        ClientType? clientType = null)
    {
        await SendPayloadToUserAsync(userId.ToString(), type, payload, GetContextIdentifier(excludeCurrentContext),
            clientType: clientType);
    }

    private async Task SendPayloadToOrganizationAsync(Guid orgId, PushType type, object payload,
        bool excludeCurrentContext, ClientType? clientType = null)
    {
        await SendPayloadToOrganizationAsync(orgId.ToString(), type, payload,
            GetContextIdentifier(excludeCurrentContext), clientType: clientType);
    }

    public async Task PushPendingSecurityTasksAsync(Guid userId)
    {
        await PushUserAsync(userId, PushType.PendingSecurityTasks);
    }

    public async Task SendPayloadToInstallationAsync(string installationId, PushType type, object payload,
        string? identifier, string? deviceId = null, ClientType? clientType = null)
    {
        var tag = BuildTag($"template:payload && installationId:{installationId}", identifier, clientType);
        await SendPayloadAsync(tag, type, payload);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    public async Task SendPayloadToUserAsync(string userId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        var tag = BuildTag($"template:payload_userId:{SanitizeTagInput(userId)}", identifier, clientType);
        await SendPayloadAsync(tag, type, payload);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    public async Task SendPayloadToOrganizationAsync(string orgId, PushType type, object payload, string? identifier,
        string? deviceId = null, ClientType? clientType = null)
    {
        var tag = BuildTag($"template:payload && organizationId:{SanitizeTagInput(orgId)}", identifier, clientType);
        await SendPayloadAsync(tag, type, payload);
        if (InstallationDeviceEntity.IsInstallationDeviceId(deviceId))
        {
            await _installationDeviceRepository.UpsertAsync(new InstallationDeviceEntity(deviceId));
        }
    }

    public async Task PushSyncOrganizationStatusAsync(Organization organization)
    {
        var message = new OrganizationStatusPushNotification
        {
            OrganizationId = organization.Id,
            Enabled = organization.Enabled
        };

        await SendPayloadToOrganizationAsync(organization.Id, PushType.SyncOrganizationStatusChanged, message, false);
    }

    public async Task PushSyncOrganizationCollectionManagementSettingsAsync(Organization organization) =>
        await SendPayloadToOrganizationAsync(
            organization.Id,
            PushType.SyncOrganizationCollectionSettingChanged,
            new OrganizationCollectionManagementPushNotification
            {
                OrganizationId = organization.Id,
                LimitCollectionCreation = organization.LimitCollectionCreation,
                LimitCollectionDeletion = organization.LimitCollectionDeletion,
                LimitItemDeletion = organization.LimitItemDeletion
            },
            false
        );

    private string? GetContextIdentifier(bool excludeCurrentContext)
    {
        if (!excludeCurrentContext)
        {
            return null;
        }

        var currentContext =
            _httpContextAccessor.HttpContext?.RequestServices.GetService(typeof(ICurrentContext)) as ICurrentContext;
        return currentContext?.DeviceIdentifier;
    }

    private string BuildTag(string tag, string? identifier, ClientType? clientType)
    {
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            tag += $" && !deviceIdentifier:{SanitizeTagInput(identifier)}";
        }

        if (clientType.HasValue && clientType.Value != ClientType.All)
        {
            tag += $" && clientType:{clientType}";
        }

        return $"({tag})";
    }

    private async Task SendPayloadAsync(string tag, PushType type, object payload)
    {
        var results = await _notificationHubPool.AllClients.SendTemplateNotificationAsync(
            new Dictionary<string, string>
            {
                { "type", ((byte)type).ToString() }, { "payload", JsonSerializer.Serialize(payload) }
            }, tag);

        if (_enableTracing)
        {
            foreach (var (client, outcome) in results)
            {
                if (!client.EnableTestSend)
                {
                    continue;
                }

                _logger.LogInformation(
                    "Azure Notification Hub Tracking ID: {Id} | {Type} push notification with {Success} successes and {Failure} failures with a payload of {@Payload} and result of {@Results}",
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
