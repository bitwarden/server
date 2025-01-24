using System.Text;
using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class RabbitMqEventHttpPostListener : RabbitMqEventListenerBase
{
    private readonly string _httpPostUrl;

    protected override string QueueName => "events-httpPost-queue";

    public RabbitMqEventHttpPostListener(
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
        : base(logger, globalSettings)
    {
        _httpPostUrl = globalSettings.RabbitMqHttpPostUrl;
    }
    protected override async Task HandleMessageAsync(EventMessage eventMessage)
    {
        using var httpClient = new HttpClient();
        var content = JsonContent.Create(eventMessage);
        var response = await httpClient.PostAsync(_httpPostUrl, content);
        response.EnsureSuccessStatusCode();
    }
}
