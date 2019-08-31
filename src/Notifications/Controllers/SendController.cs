using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications
{
    [Authorize("Internal")]
    [SelfHosted(SelfHostedOnly = true)]
    public class SendController : Controller
    {
        private readonly IHubContext<NotificationsHub> _hubContext;

        public SendController(IHubContext<NotificationsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpGet("~/alive")]
        [HttpGet("~/now")]
        [AllowAnonymous]
        [SelfHosted(SelfHostedOnly = false, NotSelfHostedOnly = false)]
        public DateTime GetAlive()
        {
            return DateTime.UtcNow;
        }

        [HttpPost("~/send")]
        public async Task PostSend()
        {
            using(var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var notificationJson = await reader.ReadToEndAsync();
                if(!string.IsNullOrWhiteSpace(notificationJson))
                {
                    await HubHelpers.SendNotificationToHubAsync(notificationJson, _hubContext);
                }
            }
        }
    }
}
