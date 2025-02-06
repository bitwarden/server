using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusEventListenerService : EventLoggingListenerService, IAsyncDisposable
{
    private readonly ILogger<AzureServiceBusEventListenerService> _logger;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusProcessor _processor;

    public AzureServiceBusEventListenerService(
        IEventMessageHandler handler,
        ILogger<AzureServiceBusEventListenerService> logger,
        GlobalSettings globalSettings,
        string subscriptionName) : base(handler)
    {
        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _processor = _client.CreateProcessor(globalSettings.EventLogging.AzureServiceBus.TopicName, subscriptionName, new ServiceBusProcessorOptions());
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor.ProcessMessageAsync += async args =>
        {
            var eventMessage = JsonSerializer.Deserialize<EventMessage>(args.Message.Body.ToString());

            await _handler.HandleEventAsync(eventMessage);
            await args.CompleteMessageAsync(args.Message);
        };

        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "An error occured while processing a message.");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1_000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync();
        await base.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.DisposeAsync();
        await _client.DisposeAsync();
        base.Dispose();
    }
}
