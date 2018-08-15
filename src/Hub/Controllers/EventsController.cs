using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Hub
{
    [Authorize("Internal")]
    public class EventsController : Controller
    {
        private readonly IHubContext<SyncHub> _syncHubContext;

        public EventsController(IHubContext<SyncHub> syncHubContext)
        {
            _syncHubContext = syncHubContext;
        }

        [HttpGet("~/events")]
        public async Task GetTest()
        {
            await _syncHubContext.Clients.All.SendAsync("ReceiveMessage", "From API.");
        }
    }
}
