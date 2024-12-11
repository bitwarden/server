using System.Text;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Bit.Notifications;

[Authorize("Internal")]
public class SendController : Controller
{
    private readonly IHubContext<NotificationsHub> _hubContext;
    private readonly IHubContext<AnonymousNotificationsHub> _anonymousHubContext;
    private readonly ILogger<SendController> _logger;

    public SendController(
        IHubContext<NotificationsHub> hubContext,
        IHubContext<AnonymousNotificationsHub> anonymousHubContext,
        ILogger<SendController> logger
    )
    {
        _hubContext = hubContext;
        _anonymousHubContext = anonymousHubContext;
        _logger = logger;
    }

    [HttpPost("~/send")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task PostSend()
    {
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
        {
            var notificationJson = await reader.ReadToEndAsync();
            if (!string.IsNullOrWhiteSpace(notificationJson))
            {
                await HubHelpers.SendNotificationToHubAsync(
                    notificationJson,
                    _hubContext,
                    _anonymousHubContext,
                    _logger
                );
            }
        }
    }
}
