#nullable enable

using System.Text;
using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusEventListenerService<TConfiguration> : EventLoggingListenerService
    where TConfiguration : IEventListenerConfiguration
{
    private readonly ServiceBusProcessor _processor;

    public AzureServiceBusEventListenerService(
        TConfiguration configuration,
        IEventMessageHandler handler,
        IAzureServiceBusService serviceBusService,
        ILoggerFactory loggerFactory)
        : base(handler, CreateLogger(loggerFactory, configuration))
    {
        _processor = serviceBusService.CreateProcessor(
            topicName: configuration.EventTopicName,
            subscriptionName: configuration.EventSubscriptionName,
            new ServiceBusProcessorOptions());
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

    private static ILogger CreateLogger(ILoggerFactory loggerFactory, TConfiguration configuration)
    {
        return loggerFactory.CreateLogger(
            categoryName: $"{typeof(AzureServiceBusEventListenerService<>).FullName}.{configuration.EventQueueName}");
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
