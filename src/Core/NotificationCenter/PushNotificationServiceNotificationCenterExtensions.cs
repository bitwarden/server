using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.NotificationCenter.Entities;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceNotificationCenterExtensions
{
    public static Task PushNotificationAsync(this IPushNotificationService service,
        Notification notification)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
#pragma warning disable BWP0001 // Type or member is obsolete
            InstallationId = notification.Global ? service.InstallationId : null,
#pragma warning restore BWP0001 // Type or member is obsolete
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
        };

        NotificationTarget target;
        Guid targetId;

        if (notification.Global)
        {
            // TODO: Think about this a bit more
            target = NotificationTarget.Installation;
#pragma warning disable BWP0001 // Type or member is obsolete
            targetId = service.InstallationId;
#pragma warning restore BWP0001 // Type or member is obsolete
        }
        else if (notification.UserId.HasValue)
        {
            target = NotificationTarget.User;
            targetId = notification.UserId.Value;
        }
        else if (notification.OrganizationId.HasValue)
        {
            target = NotificationTarget.Organization;
            targetId = notification.OrganizationId.Value;
        }
        else
        {
#pragma warning disable BWP0001 // Type or member is obsolete
            service.Logger.LogWarning("Invalid notification id {NotificationId} push notification", notification.Id);
#pragma warning restore BWP0001 // Type or member is obsolete
            return Task.CompletedTask;
        }

        return service.PushAsync(new PushNotification<NotificationPushNotification>
        {
            Type = PushType.Notification,
            Target = target,
            TargetId = targetId,
            Payload = message,
            ExcludeCurrentContext = true,
            ClientType = notification.ClientType,
        });
    }

    public static Task PushNotificationStatusAsync(this IPushNotificationService service,
        Notification notification, NotificationStatus notificationStatus)
    {
        var message = new NotificationPushNotification
        {
            Id = notification.Id,
            Priority = notification.Priority,
            Global = notification.Global,
            ClientType = notification.ClientType,
            UserId = notification.UserId,
            OrganizationId = notification.OrganizationId,
#pragma warning disable BWP0001 // Type or member is obsolete
            InstallationId = notification.Global ? service.InstallationId : null,
#pragma warning restore BWP0001 // Type or member is obsolete
            TaskId = notification.TaskId,
            Title = notification.Title,
            Body = notification.Body,
            CreationDate = notification.CreationDate,
            RevisionDate = notification.RevisionDate,
            ReadDate = notificationStatus.ReadDate,
            DeletedDate = notificationStatus.DeletedDate,
        };

        NotificationTarget target;
        Guid targetId;

        if (notification.Global)
        {
            // TODO: Think about this a bit more
            target = NotificationTarget.Installation;
#pragma warning disable BWP0001 // Type or member is obsolete
            targetId = service.InstallationId;
#pragma warning restore BWP0001 // Type or member is obsolete
        }
        else if (notification.UserId.HasValue)
        {
            target = NotificationTarget.User;
            targetId = notification.UserId.Value;
        }
        else if (notification.OrganizationId.HasValue)
        {
            target = NotificationTarget.Organization;
            targetId = notification.OrganizationId.Value;
        }
        else
        {
#pragma warning disable BWP0001 // Type or member is obsolete
            service.Logger.LogWarning("Invalid notification status id {NotificationId} push notification", notification.Id);
#pragma warning restore BWP0001 // Type or member is obsolete
            return Task.CompletedTask;
        }

        return service.PushAsync(new PushNotification<NotificationPushNotification>
        {
            Type = PushType.NotificationStatus,
            Target = target,
            TargetId = targetId,
            Payload = message,
            ExcludeCurrentContext = true,
            ClientType = notification.ClientType,
        });
    }
}
