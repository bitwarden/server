using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("slack/oauth")]
public class SlackOAuthController(ISlackService slackService) : Controller
{
    [HttpGet("redirect")]
    public IActionResult RedirectToSlack()
    {
        string callbackUrl = Url.RouteUrl(nameof(OAuthCallback));
        var redirectUrl = slackService.GetRedirectUrl(callbackUrl);

        if (string.IsNullOrEmpty(redirectUrl))
        {
            return BadRequest("Slack not currently supported.");
        }

        return Redirect(redirectUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Missing code from Slack.");
        }

        string callbackUrl = Url.RouteUrl(nameof(OAuthCallback));
        var token = await slackService.ObtainTokenViaOAuth(code, callbackUrl);

        if (string.IsNullOrEmpty(token))
        {
            return BadRequest("Invalid response from Slack.");
        }

        SaveTokenToDatabase(token);
        return Ok("Slack OAuth successful. Your bot is now installed.");
    }

    private void SaveTokenToDatabase(string botToken)
    {
        Console.WriteLine($"Stored bot token for team: {botToken}");
    }
}
