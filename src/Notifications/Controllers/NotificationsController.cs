using System.Threading.Tasks;
using Bit.Core.Models;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications
{
    [Authorize("Internal")]
    [SelfHosted(SelfHostedOnly = true)]
    public class NotificationsController : Controller
    {
        private readonly IHubContext<NotificationsHub> _hubContext;

        public NotificationsController(IHubContext<NotificationsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost("~/notifications")]
        public async Task PostNotification([FromBody]PushNotificationData<object> model)
        {
            await HubHelpers.SendNotificationToHubAsync(model, _hubContext);
        }
    }
}
