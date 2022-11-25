using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

[AllowAnonymous]
public class AnonymousNotificationsHub : Microsoft.AspNetCore.SignalR.Hub, INotificationHub
{
    public override async Task OnConnectedAsync()
    {
        var httpContext = Context.GetHttpContext();
        var token = httpContext.Request.Query["Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(token))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, token);
        }
        await base.OnConnectedAsync();
    }
}
