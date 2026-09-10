using Bit.Core.Auth.Entities;
using Bit.Core.Enums;
using Bit.Core.Models;

namespace Bit.Core.Platform.Push;

public static class PushNotificationServiceAuthExtensions
{
    public static Task PushAuthRequestAsync(this IPushNotificationService service, AuthRequest authRequest)
        => service.PushAsync(new PushNotification<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequest,
            Target = NotificationTarget.User,
            TargetId = authRequest.UserId,
            Payload = new AuthRequestPushNotification
            {
                Id = authRequest.Id,
                UserId = authRequest.UserId,
            },
            ExcludeCurrentContext = true,
        });

    public static Task PushAuthRequestResponseAsync(this IPushNotificationService service, AuthRequest authRequest)
        => service.PushAsync(new PushNotification<AuthRequestPushNotification>
        {
            Type = PushType.AuthRequestResponse,
            Target = NotificationTarget.User,
            TargetId = authRequest.UserId,
            Payload = new AuthRequestPushNotification
            {
                Id = authRequest.Id,
                UserId = authRequest.UserId,
            },
            ExcludeCurrentContext = true,
        });

    public static Task PushLogOutAsync(this IPushNotificationService service, Guid userId,
        bool excludeCurrentContextFromPush = false, PushNotificationLogOutReason? reason = null)
        => service.PushAsync(new PushNotification<LogOutPushNotification>
        {
            Type = PushType.LogOut,
            Target = NotificationTarget.User,
            TargetId = userId,
            Payload = new LogOutPushNotification
            {
                UserId = userId,
                Reason = reason,
            },
            ExcludeCurrentContext = excludeCurrentContextFromPush,
        });

    public static Task PushSyncSettingsAsync(this IPushNotificationService service, Guid userId)
        => service.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.SyncSettings,
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
