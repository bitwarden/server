using Microsoft.AspNetCore.Authorization;

namespace Bit.Notifications
{
    [AllowAnonymous]
    public class AnonymousNotificationsHub : Microsoft.AspNetCore.SignalR.Hub, INotificationHub
    {
    }
}
