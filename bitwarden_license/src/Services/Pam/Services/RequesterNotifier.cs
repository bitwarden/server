using Bit.Core.Enums;
using Bit.Core.Models;
using Bit.Core.Platform.Push;

namespace Bit.Services.Pam.Services;

public class RequesterNotifier : IRequesterNotifier
{
    private readonly IPushNotificationService _pushNotificationService;

    public RequesterNotifier(IPushNotificationService pushNotificationService)
    {
        _pushNotificationService = pushNotificationService;
    }

    public Task NotifyRequesterAsync(Guid requesterId)
    {
        return _pushNotificationService.PushAsync(new PushNotification<UserPushNotification>
        {
            Type = PushType.RefreshAccessRequest,
            Target = NotificationTarget.User,
            TargetId = requesterId,
            Payload = new UserPushNotification
            {
                UserId = requesterId,
                Date = DateTime.UtcNow,
            },
            ExcludeCurrentContext = false,
        });
    }
}
