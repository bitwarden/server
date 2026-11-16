using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models;
using Bit.Core.Enums;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceAgentConnectionExtensions
{
    public static Task PushSyncAgentConnectionCreateAsync(
        this IPushNotificationService service, AgentConnection connection) =>
        PushAsync(service, connection, PushType.SyncAgentConnectionCreate);

    public static Task PushSyncAgentConnectionUpdateAsync(
        this IPushNotificationService service, AgentConnection connection) =>
        PushAsync(service, connection, PushType.SyncAgentConnectionUpdate);

    public static Task PushSyncAgentConnectionDeleteAsync(
        this IPushNotificationService service, AgentConnection connection) =>
        PushAsync(service, connection, PushType.SyncAgentConnectionDelete);

    private static Task PushAsync(
        IPushNotificationService service, AgentConnection connection, PushType type) =>
        service.PushAsync(new PushNotification<SyncAgentConnectionPushNotification>
        {
            Type = type,
            Target = NotificationTarget.User,
            TargetId = connection.UserId,
            Payload = new SyncAgentConnectionPushNotification
            {
                Id = connection.Id,
                UserId = connection.UserId,
                RevisionDate = connection.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });
}
