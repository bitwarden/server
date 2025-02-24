using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class SlackMessageSender(
    IHttpClientFactory httpClientFactory,
    ILogger<SlackMessageSender> logger)
{
    private HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "SlackMessageSenderHttpClient";

    public async Task SendDirectMessageByEmailAsync(string token, string message, string email)
    {
        var userId = await UserIdByEmail(token, email);

        if (userId is not null)
        {
            await SendSlackDirectMessageByUserId(token, message, userId);
        }
    }

    public async Task<string> UserIdByEmail(string token, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://slack.com/api/users.lookupByEmail?email={email}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.SendAsync(request);
        var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = content.RootElement;

        if (root.GetProperty("ok").GetBoolean())
        {
            return root.GetProperty("user").GetProperty("id").GetString();
        }
        else
        {
            logger.LogError("Error retrieving slack userId: " + root.GetProperty("error").GetString());
            return null;
        }
    }

    public async Task SendSlackDirectMessageByUserId(string token, string message, string userId)
    {
        var channelId = await OpenDmChannel(token, userId);

        var payload = JsonContent.Create(new { channel = channelId, text = message });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/chat.postMessage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = payload;

        await _httpClient.SendAsync(request);
    }

    public async Task<string> OpenDmChannel(string token, string userId)
    {
        var payload = JsonContent.Create(new { users = userId });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/conversations.open");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = payload;
        var response = await _httpClient.SendAsync(request);
        var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = content.RootElement;

        if (root.GetProperty("ok").GetBoolean())
        {
            return content.RootElement.GetProperty("channel").GetProperty("id").GetString();
        }
        else
        {
            logger.LogError("Error opening DM channel: " + root.GetProperty("error").GetString());
            return null;
        }
    }
}
