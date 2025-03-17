using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class SlackService(
    IHttpClientFactory httpClientFactory,
    ILogger<SlackService> logger) : ISlackService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);

    public const string HttpClientName = "SlackServiceHttpClient";

    public async Task<string> GetChannelIdAsync(string token, string channelName)
    {
        return (await GetChannelIdsAsync(token, new List<string> { channelName }))?.FirstOrDefault();
    }

    public async Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames)
    {
        var matchingChannelIds = new List<string>();
        var baseUrl = "https://slack.com/api/conversations.list";
        var nextCursor = string.Empty;

        do
        {
            var uriBuilder = new UriBuilder(baseUrl);
            var queryParameters = HttpUtility.ParseQueryString(uriBuilder.Query);

            queryParameters["types"] = "public_channel,private_channel";
            queryParameters["limit"] = "1000";
            if (!string.IsNullOrEmpty(nextCursor))
            {
                queryParameters["cursor"] = nextCursor;
            }

            uriBuilder.Query = queryParameters.ToString();

            var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var jsonResponse = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = jsonResponse.RootElement;
            if (root.GetProperty("ok").GetBoolean())
            {
                var channelList = root.GetProperty("channels");

                foreach (var channel in channelList.EnumerateArray())
                {
                    if (channelNames.Contains(channel.GetProperty("name").GetString() ?? string.Empty))
                    {
                        matchingChannelIds.Add(channel.GetProperty("id").GetString() ?? string.Empty);
                    }
                }

                if (root.TryGetProperty("response_metadata", out var metadata))
                {
                    nextCursor = metadata.GetProperty("next_cursor").GetString() ?? string.Empty;
                }
                else
                {
                    nextCursor = string.Empty;
                }
            }
            else
            {
                logger.LogError("Error retrieving slack userId: " + root.GetProperty("error").GetString());
                break;
            }
        } while (!string.IsNullOrEmpty(nextCursor));

        return matchingChannelIds;
    }

    public async Task<string> GetDmChannelByEmailAsync(string token, string email)
    {
        var userId = await GetUserIdByEmailAsync(token, email);
        return await OpenDmChannel(token, userId);
    }

    public async Task SendSlackMessageByChannelId(string token, string message, string channelId)
    {
        var payload = JsonContent.Create(new { channel = channelId, text = message });
        var request = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/chat.postMessage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = payload;

        await _httpClient.SendAsync(request);
    }

    private async Task SendDirectMessageByEmailAsync(string token, string message, string email)
    {
        var channelId = await GetDmChannelByEmailAsync(token, email);
        if (!string.IsNullOrEmpty(channelId))
        {
            await SendSlackMessageByChannelId(token, message, channelId);
        }
    }

    private async Task<string> GetUserIdByEmailAsync(string token, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"https://slack.com/api/users.lookupByEmail?email={email}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.SendAsync(request);
        var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = content.RootElement;

        if (root.GetProperty("ok").GetBoolean())
        {
            return root.GetProperty("user").GetProperty("id").GetString() ?? string.Empty;
        }
        else
        {
            logger.LogError("Error retrieving slack userId: " + root.GetProperty("error").GetString());
            return string.Empty;
        }
    }

    private async Task<string> OpenDmChannel(string token, string userId)
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
            return content.RootElement.GetProperty("channel").GetProperty("id").GetString() ?? string.Empty;
        }
        else
        {
            logger.LogError("Error opening DM channel: " + root.GetProperty("error").GetString());
            return string.Empty;
        }
    }
}
