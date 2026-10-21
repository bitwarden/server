#nullable enable
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Notifications.Controllers;

[ApiController]
[Authorize("Internal")]
public class SendController : Controller
{
    private readonly HubHelpers _hubHelpers;

    public SendController(HubHelpers hubHelpers)
    {
        _hubHelpers = hubHelpers;
    }

    [HttpPost("~/send")]
    [SelfHosted(SelfHostedOnly = true)]
    public async Task PostSendAsync(InboundNotification inboundNotification)
    {
        await _hubHelpers.SendNotificationToHubAsync(inboundNotification, HttpContext.RequestAborted);
    }
}
