using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusEventListenerService : EventLoggingListenerService
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

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var eventMessage = JsonSerializer.Deserialize<EventMessage>(args.Message.Body.ToString());

                await _handler.HandleEventAsync(eventMessage);
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "An error occured while processing message: {MessageId}",
                    args.Message.MessageId
                );
            }
        };

        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(
                args.Exception,
                "An error occurred. Entity Path: {EntityPath}, Error Source: {ErrorSource}",
                args.EntityPath,
                args.ErrorSource
            );
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _processor.DisposeAsync().GetAwaiter().GetResult();
        _client.DisposeAsync().GetAwaiter().GetResult();
        base.Dispose();
    }
}
