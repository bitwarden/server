using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class WebhookEventHandler : IEventMessageHandler
{
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;

    public const string HttpClientName = "WebhookEventHandlerHttpClient";

    public WebhookEventHandler(
        IHttpClientFactory httpClientFactory,
        GlobalSettings globalSettings)
    {
        _httpClient = httpClientFactory.CreateClient(HttpClientName);
        _webhookUrl = globalSettings.EventLogging.WebhookUrl;
    }

    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var content = JsonContent.Create(eventMessage);
        var response = await _httpClient.PostAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
