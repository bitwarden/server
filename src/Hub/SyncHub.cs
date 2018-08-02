using System;
using System.Threading.Tasks;
using Bit.Core;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Hub
{
    [Authorize("Application")]
    public class SyncHub : Microsoft.AspNetCore.SignalR.Hub
    {
        public override async Task OnConnectedAsync()
        {
            var currentContext = new CurrentContext();
            currentContext.Build(Context.User);
            foreach(var org in currentContext.Organizations)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"Organization_{org.Id}");
            }
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
            await base.OnDisconnectedAsync(exception);
        }
    }
}
