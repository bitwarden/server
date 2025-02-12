using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;

namespace Bit.Core.Services;

public class WebhookEventHandler(
    IHttpClientFactory httpClientFactory,
    GlobalSettings globalSettings)
    : IEventMessageHandler
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(HttpClientName);
    private readonly string _webhookUrl = globalSettings.EventLogging.WebhookUrl;

    public const string HttpClientName = "WebhookEventHandlerHttpClient";

    public async Task HandleEventAsync(EventMessage eventMessage)
    {
        var content = JsonContent.Create(eventMessage);
        var response = await _httpClient.PostAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }

    public async Task HandleManyEventAsync(IEnumerable<EventMessage> eventMessages)
    {
        var content = JsonContent.Create(eventMessages);
        var response = await _httpClient.PostAsync(_webhookUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
