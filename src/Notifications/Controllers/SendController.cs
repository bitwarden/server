#nullable enable
using System.Text;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Notifications.Controllers;

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
    public async Task PostSendAsync()
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var notificationJson = await reader.ReadToEndAsync();
        if (!string.IsNullOrWhiteSpace(notificationJson))
        {
            await _hubHelpers.SendNotificationToHubAsync(notificationJson);
        }
    }
}
