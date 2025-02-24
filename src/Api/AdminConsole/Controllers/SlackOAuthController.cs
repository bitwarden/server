using System.Text.Json;
using Bit.Core.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Bit.Api.AdminConsole.Controllers;

[Route("slack/oauth")]
public class SlackOAuthController(
    IHttpClientFactory httpClientFactory,
    GlobalSettings globalSettings)
    : Controller
{
    private readonly string _clientId = globalSettings.Slack.ClientId;
    private readonly string _clientSecret = globalSettings.Slack.ClientSecret;
    private readonly string _scopes = globalSettings.Slack.Scopes;
    private readonly string _redirectUrl = globalSettings.Slack.RedirectUrl;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "SlackOAuthContollerHttpClient";

    [HttpGet("redirect")]
    public IActionResult RedirectToSlack()
    {
        string slackOAuthUrl = $"https://slack.com/oauth/v2/authorize?client_id={_clientId}&scope={_scopes}&redirect_uri={_redirectUrl}";

        return Redirect(slackOAuthUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> OAuthCallback([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest("Missing code from Slack.");
        }

        var tokenResponse = await _httpClient.PostAsync("https://slack.com/api/oauth.v2.access",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", _redirectUrl)
            }));

        var responseBody = await tokenResponse.Content.ReadAsStringAsync();
        var jsonDoc = JsonDocument.Parse(responseBody);
        var root = jsonDoc.RootElement;

        if (!root.GetProperty("ok").GetBoolean())
        {
            return BadRequest($"OAuth failed: {root.GetProperty("error").GetString()}");
        }

        string botToken = root.GetProperty("access_token").GetString();
        string teamId = root.GetProperty("team").GetProperty("id").GetString();

        SaveTokenToDatabase(teamId, botToken);

        return Ok("Slack OAuth successful. Your bot is now installed.");
    }

    private void SaveTokenToDatabase(string teamId, string botToken)
    {
        Console.WriteLine($"Stored bot token for team {teamId}: {botToken}");
    }
}
