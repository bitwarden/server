using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class HttpPostEventHandler : IEventMessageHandler
{
    private readonly HttpClient _httpClient;
    private readonly string _httpPostUrl;

    public const string HttpClientName = "HttpPostEventHandlerHttpClient";

    public HttpPostEventHandler(
        IHttpClientFactory httpClientFactory,
        GlobalSettings globalSettings)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _httpPostUrl = globalSettings.EventLogging.HttpPostUrl;
    }

    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var content = JsonContent.Create(eventMessage);
        var response = await _httpClient.PostAsync(_httpPostUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
