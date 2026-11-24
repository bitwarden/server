using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models;
using Bit.Core.Enums;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceConnectedAppExtensions
{
    public static Task PushSyncConnectedAppCreateAsync(
        this IPushNotificationService service, ConnectedApp connection) =>
        PushAsync(service, connection, PushType.SyncConnectedAppCreate);

    public static Task PushSyncConnectedAppUpdateAsync(
        this IPushNotificationService service, ConnectedApp connection) =>
        PushAsync(service, connection, PushType.SyncConnectedAppUpdate);

    public static Task PushSyncConnectedAppDeleteAsync(
        this IPushNotificationService service, ConnectedApp connection) =>
        PushAsync(service, connection, PushType.SyncConnectedAppDelete);

    private static Task PushAsync(
        IPushNotificationService service, ConnectedApp connection, PushType type) =>
        service.PushAsync(new PushNotification<SyncConnectedAppPushNotification>
        {
            Type = type,
            Target = NotificationTarget.User,
            TargetId = connection.UserId,
            Payload = new SyncConnectedAppPushNotification
            {
                Id = connection.Id,
                UserId = connection.UserId,
                RevisionDate = connection.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });
}
