using Bit.Core.Context;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Notifications;

[Authorize("Application")]
public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub
{
    private readonly ConnectionCounter _connectionCounter;
    private readonly GlobalSettings _globalSettings;

    public NotificationsHub(ConnectionCounter connectionCounter, GlobalSettings globalSettings)
    {
        _connectionCounter = connectionCounter;
        _globalSettings = globalSettings;
    }

    public override async Task OnConnectedAsync()
    {
        var currentContext = new CurrentContext(null);
        await currentContext.BuildAsync(Context.User, _globalSettings);
        if (currentContext.Organizations != null)
        {
            foreach (var org in currentContext.Organizations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Organization_{org.Id}");
            }
        }
        _connectionCounter.Increment();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception exception)
    {
        var currentContext = new CurrentContext(null);
        await currentContext.BuildAsync(Context.User, _globalSettings);
        if (currentContext.Organizations != null)
        {
            foreach (var org in currentContext.Organizations)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Organization_{org.Id}");
            }
        }
        _connectionCounter.Decrement();
        await base.OnDisconnectedAsync(exception);
    }
}
