using System.Net.Http.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class RabbitMqEventHttpPostListener : RabbitMqEventListenerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _httpPostUrl;
    private readonly string _queueName;

    protected override string QueueName => _queueName;

    public const string HttpClientName = "EventHttpPostListenerHttpClient";

    public RabbitMqEventHttpPostListener(
        IHttpClientFactory httpClientFactory,
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
        : base(logger, globalSettings)
    {
        _httpClientFactory = httpClientFactory;
        _httpPostUrl = globalSettings.EventLogging.RabbitMq.HttpPostUrl;
        _queueName = globalSettings.EventLogging.RabbitMq.HttpPostQueueName;
    }

    protected override async Task HandleMessageAsync(EventMessage eventMessage)
    {
        using var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        var content = JsonContent.Create(eventMessage);
        var response = await httpClient.PostAsync(_httpPostUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
