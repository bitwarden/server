using Azure.Messaging.ServiceBus;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusIntegrationListenerService : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly string _subscriptionName;
    private readonly string _topicName;
    private readonly int _maxRetries;
    private readonly IIntegrationHandler _handler;
    private readonly ServiceBusSender _sender;
    private readonly ILogger<AzureServiceBusIntegrationListenerService> _logger;
    private ServiceBusProcessor _processor;

    public AzureServiceBusIntegrationListenerService(
        IIntegrationHandler handler,
        string subscriptionName,
        GlobalSettings globalSettings,
        ILogger<AzureServiceBusIntegrationListenerService> logger)
    {
        _handler = handler;
        _logger = logger;
        _maxRetries = globalSettings.EventLogging.AzureServiceBus.MaxRetries;
        _topicName = globalSettings.EventLogging.AzureServiceBus.IntegrationTopicName;
        _subscriptionName = subscriptionName;

        _client = new ServiceBusClient(globalSettings.EventLogging.AzureServiceBus.ConnectionString);
        _sender = _client.CreateSender(_topicName);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _processor = _client.CreateProcessor(_topicName, _subscriptionName, new ServiceBusProcessorOptions
        {
            MaxConcurrentCalls = 1,
            AutoCompleteMessages = false
        });

        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Azure Service Bus error");
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await _sender.DisposeAsync();
        await _client.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var json = args.Message.Body.ToString();

        try
        {
            var result = await _handler.HandleAsync(json);
            var message = result.Message;

            if (result.Success)
            {
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            message.ApplyRetry(result.DelayUntilDate);

            if (result.Retryable && message.RetryCount < _maxRetries)
            {
                var scheduledTime = message.DelayUntilDate ?? DateTime.UtcNow.AddSeconds(10);
                var retryMsg = new ServiceBusMessage(message.ToJson())
                {
                    Subject = args.Message.Subject,
                    ScheduledEnqueueTime = scheduledTime
                };

                await _sender.SendMessageAsync(retryMsg);
            }
            else
            {
                await args.DeadLetterMessageAsync(args.Message, "Retry limit exceeded or non-retryable");
                return;
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing ASB message");
            await args.CompleteMessageAsync(args.Message);
        }
    }
}
