using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public class HubHelpers
{
    private static readonly JsonSerializerOptions _deserializerOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly string _receiveMessageMethod = "ReceiveMessage";

    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IHubContext<AnonymousNotificationsHub> _anonymousHubContext;
    private readonly ILogger<HubHelpers> _logger;

    public HubHelpers(IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        ILogger<HubHelpers> logger)
    {
        _hubContext = hubContext;
        _anonymousHubContext = anonymousHubContext;
        _logger = logger;
    }

    public async Task SendNotificationToHubAsync(string notificationJson, CancellationToken cancellationToken = default)
    {
        var notification =
            JsonSerializer.Deserialize<PushNotificationData<object>>(notificationJson, _deserializerOptions);
        _logger.LogInformation("Sending notification: {NotificationType}", notification.Type);
        switch (notification.Type)
        {
            case PushType.SyncCipherUpdate:
            case PushType.SyncCipherCreate:
            case PushType.SyncCipherDelete:
            case PushType.SyncLoginDelete:
                var cipherNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncCipherPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (cipherNotification.Payload.UserId.HasValue)
                {
                    await _hubContext.Clients.User(cipherNotification.Payload.UserId.ToString())
                        .SendAsync(_receiveMessageMethod, cipherNotification, cancellationToken);
                }
                else if (cipherNotification.Payload.OrganizationId.HasValue)
                {
                    await _hubContext.Clients
                        .Group(NotificationsHub.GetOrganizationGroup(cipherNotification.Payload.OrganizationId.Value))
                        .SendAsync(_receiveMessageMethod, cipherNotification, cancellationToken);
                }

                break;
            case PushType.SyncFolderUpdate:
            case PushType.SyncFolderCreate:
            case PushType.SyncFolderDelete:
                var folderNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncFolderPushNotification>>(
                        notificationJson, _deserializerOptions);
                await _hubContext.Clients.User(folderNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, folderNotification, cancellationToken);
                break;
            case PushType.SyncCiphers:
            case PushType.SyncVault:
            case PushType.SyncOrganizations:
            case PushType.SyncOrgKeys:
            case PushType.SyncSettings:
            case PushType.LogOut:
                var userNotification =
                    JsonSerializer.Deserialize<PushNotificationData<UserPushNotification>>(
                        notificationJson, _deserializerOptions);
                await _hubContext.Clients.User(userNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, userNotification, cancellationToken);
                break;
            case PushType.SyncSendCreate:
            case PushType.SyncSendUpdate:
            case PushType.SyncSendDelete:
                var sendNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncSendPushNotification>>(
                        notificationJson, _deserializerOptions);
                await _hubContext.Clients.User(sendNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, sendNotification, cancellationToken);
                break;
            case PushType.AuthRequestResponse:
                var authRequestResponseNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                await _anonymousHubContext.Clients.Group(authRequestResponseNotification.Payload.Id.ToString())
                    .SendAsync("AuthRequestResponseRecieved", authRequestResponseNotification, cancellationToken);
                break;
            case PushType.AuthRequest:
                var authRequestNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                await _hubContext.Clients.User(authRequestNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, authRequestNotification, cancellationToken);
                break;
            case PushType.SyncNotification:
                var syncNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncNotificationPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (syncNotification.Payload.UserId.HasValue)
                {
                    if (syncNotification.Payload.ClientType == ClientType.All)
                    {
                        await _hubContext.Clients.User(syncNotification.Payload.UserId.ToString())
                            .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                    }
                    else
                    {
                        await _hubContext.Clients.Group(NotificationsHub.GetUserGroup(
                                syncNotification.Payload.UserId.Value, syncNotification.Payload.ClientType))
                            .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                    }
                }
                else if (syncNotification.Payload.OrganizationId.HasValue)
                {
                    await _hubContext.Clients.Group(NotificationsHub.GetOrganizationGroup(
                            syncNotification.Payload.OrganizationId.Value, syncNotification.Payload.ClientType))
                        .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                }

                break;
            default:
                break;
        }
    }
}
