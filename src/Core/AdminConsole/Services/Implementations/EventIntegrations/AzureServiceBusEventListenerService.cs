#nullable enable

using System.Text;
using Azure.Messaging.ServiceBus;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusEventListenerService : EventLoggingListenerService
{
    private readonly ServiceBusProcessor _processor;

    public AzureServiceBusEventListenerService(
        IEventMessageHandler handler,
        IAzureServiceBusService serviceBusService,
        string subscriptionName,
        GlobalSettings globalSettings,
        ILogger<AzureServiceBusEventListenerService> logger) : base(handler, logger)
    {
        _processor = serviceBusService.CreateProcessor(
            globalSettings.EventLogging.AzureServiceBus.EventTopicName,
            subscriptionName,
            new ServiceBusProcessorOptions());
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _processor.ProcessMessageAsync += ProcessReceivedMessageAsync;
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _processor.StopProcessingAsync(cancellationToken);
        await _processor.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    internal Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger.LogError(
            args.Exception,
            "An error occurred. Entity Path: {EntityPath}, Error Source: {ErrorSource}",
            args.EntityPath,
            args.ErrorSource
        );
        return Task.CompletedTask;
    }

    private async Task ProcessReceivedMessageAsync(ProcessMessageEventArgs args)
    {
        await ProcessReceivedMessageAsync(Encoding.UTF8.GetString(args.Message.Body), args.Message.MessageId);
        await args.CompleteMessageAsync(args.Message);
    }
}
