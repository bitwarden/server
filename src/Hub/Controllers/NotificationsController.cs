using System.Threading.Tasks;
using Bit.Core.Models;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Hub
{
    [Authorize("Internal")]
    [SelfHosted(SelfHostedOnly = true)]
    public class NotificationsController : Controller
    {
        private readonly IHubContext<SyncHub> _syncHubContext;

        public NotificationsController(IHubContext<SyncHub> syncHubContext)
        {
            _syncHubContext = syncHubContext;
        }

        [HttpPost("~/notifications")]
        public async Task PostNotification([FromBody]PushNotificationData<object> model)
        {
            await HubHelpers.SendNotificationToHubAsync(model, _syncHubContext);
        }
    }
}
