using Bit.Core.Platform.Push;

namespace Bit.Commercial.Pam.Services;

public class RequesterNotifier : IRequesterNotifier
{
    private readonly IPushNotificationService _pushNotificationService;

    public RequesterNotifier(IPushNotificationService pushNotificationService)
    {
        _pushNotificationService = pushNotificationService;
    }

    public Task NotifyRequesterAsync(Guid requesterId)
    {
        return _pushNotificationService.PushRefreshAccessRequestAsync(requesterId);
    }
}
