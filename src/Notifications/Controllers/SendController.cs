using System.IO;
using System.Text;
using System.Threading.Tasks;
using Bit.Core.Models;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;

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

        [HttpPost("~/send")]
        public async Task PostSend()
        {
            using(var reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var notificationJson = await reader.ReadToEndAsync();
                if(!string.IsNullOrWhiteSpace(notificationJson))
                {
                    var notification = JsonConvert.DeserializeObject<PushNotificationData<object>>(notificationJson);
                    await HubHelpers.SendNotificationToHubAsync(notification.Type, notificationJson, _hubContext);
                }
            }
        }
    }
}
