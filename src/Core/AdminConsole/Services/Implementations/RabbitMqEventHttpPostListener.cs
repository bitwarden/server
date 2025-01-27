using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class RabbitMqEventHttpPostListener : RabbitMqEventListenerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _httpPostUrl;

    protected override string QueueName => "events-httpPost-queue";

    public const string HttpClientName = "EventHttpPostListenerHttpClient";

    public RabbitMqEventHttpPostListener(
        IHttpClientFactory httpClientFactory,
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
        : base(logger, globalSettings)
    {
        _httpClientFactory = httpClientFactory;
        _httpPostUrl = globalSettings.RabbitMqHttpPostUrl;
    }
    protected override async Task HandleMessageAsync(EventMessage eventMessage)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var content = JsonContent.Create(eventMessage);
        var response = await httpClient.PostAsync(_httpPostUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
