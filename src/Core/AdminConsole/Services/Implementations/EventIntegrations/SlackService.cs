using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Bit.Core.Models.Slack;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class SlackService(
    IHttpClientFactory httpClientFactory,
    GlobalSettings globalSettings,
    ILogger<SlackService> logger) : ISlackService
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);
    private readonly string _clientId = globalSettings.Slack.ClientId;
    private readonly string _clientSecret = globalSettings.Slack.ClientSecret;
    private readonly string _scopes = globalSettings.Slack.Scopes;
    private readonly string _slackApiBaseUrl = globalSettings.Slack.ApiBaseUrl;

    public const string HttpClientName = "SlackServiceHttpClient";

    public async Task<string> GetChannelIdAsync(string token, string channelName)
    {
        return (await GetChannelIdsAsync(token, [channelName])).FirstOrDefault() ?? string.Empty;
    }

    public async Task<List<string>> GetChannelIdsAsync(string token, List<string> channelNames)
    {
        var matchingChannelIds = new List<string>();
        var baseUrl = $"{_slackApiBaseUrl}/conversations.list";
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
            var result = await response.Content.ReadFromJsonAsync<SlackChannelListResponse>();

            if (result is { Ok: true })
            {
                matchingChannelIds.AddRange(result.Channels
                    .Where(channel => channelNames.Contains(channel.Name))
                    .Select(channel => channel.Id));
                nextCursor = result.ResponseMetadata.NextCursor;
            }
            else
            {
                logger.LogError("Error getting Channel Ids: {Error}", result?.Error ?? "Unknown Error");
                nextCursor = string.Empty;
            }

        } while (!string.IsNullOrEmpty(nextCursor));

        return matchingChannelIds;
    }

    public async Task<string> GetDmChannelByEmailAsync(string token, string email)
    {
        var userId = await GetUserIdByEmailAsync(token, email);
        return await OpenDmChannel(token, userId);
    }

    public string GetRedirectUrl(string redirectUrl)
    {
        return $"https://slack.com/oauth/v2/authorize?client_id={_clientId}&scope={_scopes}&redirect_uri={redirectUrl}";
    }

    public async Task<string> ObtainTokenViaOAuth(string code, string redirectUrl)
    {
        var tokenResponse = await _httpClient.PostAsync($"{_slackApiBaseUrl}/oauth.v2.access",
            new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("client_id", _clientId),
                new KeyValuePair<string, string>("client_secret", _clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUrl)
            }));

        SlackOAuthResponse? result;
        try
        {
            result = await tokenResponse.Content.ReadFromJsonAsync<SlackOAuthResponse>();
        }
        catch
        {
            result = null;
        }

        if (result is null)
        {
            logger.LogError("Error obtaining token via OAuth: Unknown error");
            return string.Empty;
        }
        if (!result.Ok)
        {
            logger.LogError("Error obtaining token via OAuth: {Error}", result.Error);
            return string.Empty;
        }

        return result.AccessToken;
    }

    public async Task SendSlackMessageByChannelIdAsync(string token, string message, string channelId)
    {
        var payload = JsonContent.Create(new { channel = channelId, text = message });
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_slackApiBaseUrl}/chat.postMessage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = payload;

        await _httpClient.SendAsync(request);
    }

    private async Task<string> GetUserIdByEmailAsync(string token, string email)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_slackApiBaseUrl}/users.lookupByEmail?email={email}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _httpClient.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<SlackUserResponse>();

        if (result is null)
        {
            logger.LogError("Error retrieving Slack user ID: Unknown error");
            return string.Empty;
        }
        if (!result.Ok)
        {
            logger.LogError("Error retrieving Slack user ID: {Error}", result.Error);
            return string.Empty;
        }

        return result.User.Id;
    }

    private async Task<string> OpenDmChannel(string token, string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return string.Empty;

        var payload = JsonContent.Create(new { users = userId });
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_slackApiBaseUrl}/conversations.open");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = payload;
        var response = await _httpClient.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<SlackDmResponse>();

        if (result is null)
        {
            logger.LogError("Error opening DM channel: Unknown error");
            return string.Empty;
        }
        if (!result.Ok)
        {
            logger.LogError("Error opening DM channel: {Error}", result.Error);
            return string.Empty;
        }

        return result.Channel.Id;
    }
}
