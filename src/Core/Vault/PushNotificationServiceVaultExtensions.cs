using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Vault.Entities;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceVaultExtensions
{
    public static Task PushSyncFolderCreateAsync(this IPushNotificationService service, Folder folder)
        => service.PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderCreate,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    public static Task PushSyncFolderUpdateAsync(this IPushNotificationService service, Folder folder)
        => service.PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderUpdate,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    public static Task PushSyncFolderDeleteAsync(this IPushNotificationService service, Folder folder)
        => service.PushAsync(new PushNotification<SyncFolderPushNotification>
        {
            Type = PushType.SyncFolderDelete,
            Target = NotificationTarget.User,
            TargetId = folder.UserId,
            Payload = new SyncFolderPushNotification
            {
                Id = folder.Id,
                UserId = folder.UserId,
                RevisionDate = folder.RevisionDate,
            },
            ExcludeCurrentContext = true,
        });

    public static Task PushSyncCiphersAsync(this IPushNotificationService service, Guid userId,
        bool excludeCurrentContext = false)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncCiphers,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = excludeCurrentContext,
        });

    public static Task PushSyncVaultAsync(this IPushNotificationService service, Guid userId)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncVault,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = false,
        });

    public static Task PushRefreshSecurityTasksAsync(this IPushNotificationService service, Guid userId)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.RefreshSecurityTasks,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new UserPushNotification
            {
                UserId = userId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = false,
        });
}
