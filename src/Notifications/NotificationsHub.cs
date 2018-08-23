using System;
using System.Threading.Tasks;
using Bit.Core;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Notifications
{
    [Authorize("Application")]
    public class NotificationsHub : Microsoft.AspNetCore.SignalR.Hub
    {
        private readonly ConnectionCounter _connectionCounter;

        public NotificationsHub(ConnectionCounter connectionCounter)
        {
            _connectionCounter = connectionCounter;
        }

        public override async Task OnConnectedAsync()
        {
            var currentContext = new CurrentContext();
            currentContext.Build(Context.User);
            foreach(var org in currentContext.Organizations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Organization_{org.Id}");
            }
            _connectionCounter.Increment();
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var currentContext = new CurrentContext();
            currentContext.Build(Context.User);
            foreach(var org in currentContext.Organizations)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Organization_{org.Id}");
            }
            _connectionCounter.Decrement();
            await base.OnDisconnectedAsync(exception);
        }
    }
}
