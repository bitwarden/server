using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Tools.Entities;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceToolsExtensions
{
    public static Task PushSyncSendCreateAsync(this IPushNotificationService service, Send send)
    {
        if (send.UserId.HasValue)
        {
            return service.PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendCreate,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }

    public static Task PushSyncSendUpdateAsync(this IPushNotificationService service, Send send)
    {
        if (send.UserId.HasValue)
        {
            return service.PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendUpdate,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }

    public static Task PushSyncSendDeleteAsync(this IPushNotificationService service, Send send)
    {
        if (send.UserId.HasValue)
        {
            return service.PushAsync(new PushNotification<SyncSendPushNotification>
            {
                Type = PushType.SyncSendDelete,
                Target = NotificationTarget.User,
                TargetId = send.UserId.Value,
                Payload = new SyncSendPushNotification
                {
                    Id = send.Id,
                    UserId = send.UserId.Value,
                    RevisionDate = send.RevisionDate,
                },
                ExcludeCurrentContext = true,
            });
        }

        return Task.CompletedTask;
    }
}
