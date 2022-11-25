using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

public static class HubHelpers
{
    private static JsonSerializerOptions _deserializerOptions =
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public static async Task SendNotificationToHubAsync(
        string notificationJson,
        IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        CancellationToken cancellationToken = default(CancellationToken)
    )
    {
        var notification = JsonSerializer.Deserialize<PushNotificationData<object>>(notificationJson);
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
                        .SendAsync("ReceiveMessage", cipherNotification, cancellationToken);
                }
                else if (cipherNotification.Payload.OrganizationId.HasValue)
                {
                    await hubContext.Clients.Group(
                        $"Organization_{cipherNotification.Payload.OrganizationId}")
                        .SendAsync("ReceiveMessage", cipherNotification, cancellationToken);
                }
                break;
            case PushType.SyncFolderUpdate:
            case PushType.SyncFolderCreate:
            case PushType.SyncFolderDelete:
                var folderNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncFolderPushNotification>>(
                        notificationJson, _deserializerOptions);
                await hubContext.Clients.User(folderNotification.Payload.UserId.ToString())
                        .SendAsync("ReceiveMessage", folderNotification, cancellationToken);
                break;
            case PushType.SyncCiphers:
            case PushType.SyncVault:
            case PushType.SyncOrgKeys:
            case PushType.SyncSettings:
            case PushType.LogOut:
                var userNotification =
                    JsonSerializer.Deserialize<PushNotificationData<UserPushNotification>>(
                        notificationJson, _deserializerOptions);
                await hubContext.Clients.User(userNotification.Payload.UserId.ToString())
                        .SendAsync("ReceiveMessage", userNotification, cancellationToken);
                break;
            case PushType.SyncSendCreate:
            case PushType.SyncSendUpdate:
            case PushType.SyncSendDelete:
                var sendNotification =
                    JsonSerializer.Deserialize<PushNotificationData<SyncSendPushNotification>>(
                            notificationJson, _deserializerOptions);
                await hubContext.Clients.User(sendNotification.Payload.UserId.ToString())
                    .SendAsync("ReceiveMessage", sendNotification, cancellationToken);
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
                    .SendAsync("ReceiveMessage", authRequestNotification, cancellationToken);
                break;
            default:
                break;
        }
    }
}
