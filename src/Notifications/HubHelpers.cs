using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public static class HubHelpers
{
    private static JsonSerializerOptions _deserializerOptions =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    private static readonly string _receiveMessageMethod = "ReceiveMessage";

    public static async Task SendNotificationToHubAsync(
        string notificationJson,
        IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        ILogger logger,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var notification =
            JsonSerializer.Deserialize<PushNotificationData<object>>(notificationJson, _deserializerOptions);
        logger.LogInformation("Sending notification: {NotificationType}", notification.Type);
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
                    await hubContext.Clients.User(cipherNotification.Payload.UserId.ToString())
                        .SendAsync(_receiveMessageMethod, cipherNotification, cancellationToken);
                }
                else if (cipherNotification.Payload.OrganizationId.HasValue)
                {
                    await hubContext.Clients
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
                await hubContext.Clients.User(folderNotification.Payload.UserId.ToString())
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
                await hubContext.Clients.User(userNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, userNotification, cancellationToken);
                break;
            case PushType.SyncSendCreate:
            case PushType.SyncSendUpdate:
            case PushType.SyncSendDelete:
                var sendNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncSendPushNotification>>(
                        notificationJson, _deserializerOptions);
                await hubContext.Clients.User(sendNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, sendNotification, cancellationToken);
                break;
            case PushType.AuthRequestResponse:
                var authRequestResponseNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                await anonymousHubContext.Clients.Group(authRequestResponseNotification.Payload.Id.ToString())
                    .SendAsync("AuthRequestResponseRecieved", authRequestResponseNotification, cancellationToken);
                break;
            case PushType.AuthRequest:
                var authRequestNotification =
                    JsonSerializer.Deserialize<PushNotificationData<AuthRequestPushNotification>>(
                        notificationJson, _deserializerOptions);
                await hubContext.Clients.User(authRequestNotification.Payload.UserId.ToString())
                    .SendAsync(_receiveMessageMethod, authRequestNotification, cancellationToken);
                break;
            case PushType.SyncNotification:
                var syncNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncNotificationPushNotification>>(
                        notificationJson, _deserializerOptions);
                if (syncNotification.Payload.Global)
                {
                    if (syncNotification.Payload.ClientType == ClientType.All)
                    {
                        await hubContext.Clients.All.SendAsync(_receiveMessageMethod, syncNotification,
                            cancellationToken);
                    }
                    else
                    {
                        await hubContext.Clients
                            .Group(NotificationsHub.GetGlobalGroup(syncNotification.Payload.ClientType))
                            .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                    }
                }
                else if (syncNotification.Payload.UserId.HasValue)
                {
                    if (syncNotification.Payload.ClientType == ClientType.All)
                    {
                        await hubContext.Clients.User(syncNotification.Payload.UserId.ToString())
                            .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                    }
                    else
                    {
                        await hubContext.Clients.Group(NotificationsHub.GetUserGroup(
                                syncNotification.Payload.UserId.Value, syncNotification.Payload.ClientType))
                            .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                    }
                }
                else if (syncNotification.Payload.OrganizationId.HasValue)
                {
                    await hubContext.Clients.Group(NotificationsHub.GetOrganizationGroup(
                            syncNotification.Payload.OrganizationId.Value, syncNotification.Payload.ClientType))
                        .SendAsync(_receiveMessageMethod, syncNotification, cancellationToken);
                }

                break;
            default:
                break;
        }
    }
}
