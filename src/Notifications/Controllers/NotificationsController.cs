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
    public class NotificationsController : Controller
    {
        private readonly IHubContext<NotificationsHub> _hubContext;

        public NotificationsController(IHubContext<NotificationsHub> hubContext)
        {
            _hubContext = hubContext;
        }

        [HttpPost("~/notifications")]
        public async Task PostNotification()
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
