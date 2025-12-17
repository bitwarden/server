using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

public class AzureServiceBusIntegrationListenerService<TConfiguration> : BackgroundService
    where TConfiguration : IIntegrationListenerConfiguration
{
    private readonly int _maxRetries;
    private readonly IAzureServiceBusService _serviceBusService;
    private readonly IIntegrationHandler _handler;
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger _logger;

    public AzureServiceBusIntegrationListenerService(
        TConfiguration configuration,
        IIntegrationHandler handler,
        IAzureServiceBusService serviceBusService,
        ServiceBusProcessorOptions serviceBusOptions,
        ILoggerFactory loggerFactory)
    {
        _handler = handler;
        _logger = loggerFactory.CreateLogger(
            categoryName: $"Bit.Core.Services.AzureServiceBusIntegrationListenerService.{configuration.IntegrationSubscriptionName}");
        _maxRetries = configuration.MaxRetries;
        _serviceBusService = serviceBusService;

        _processor = _serviceBusService.CreateProcessor(
            topicName: configuration.IntegrationTopicName,
            subscriptionName: configuration.IntegrationSubscriptionName,
            options: serviceBusOptions);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _processor.ProcessMessageAsync += HandleMessageAsync;
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

    internal async Task<bool> HandleMessageAsync(string body)
    {
        try
        {
            var result = await _handler.HandleAsync(body);
            var message = result.Message;

            if (result.Success)
            {
                // Successful integration. Return true to indicate the message has been handled
                return true;
            }

            message.ApplyRetry(result.DelayUntilDate);

            if (result.Retryable && message.RetryCount < _maxRetries)
            {
                // Publish message to the retry queue. It will be re-published for retry after a delay
                // Return true to indicate the message has been handled
                await _serviceBusService.PublishToRetryAsync(message);
                return true;
            }
            else
            {
                // Non-recoverable failure or exceeded the max number of retries
                // Return false to indicate this message should be dead-lettered
                _logger.LogWarning(
                    "Integration failure - non-recoverable error or max retries exceeded. " +
                    "MessageId: {MessageId}, IntegrationType: {IntegrationType}, OrganizationId: {OrgId}, " +
                    "FailureCategory: {Category}, Reason: {Reason}, RetryCount: {RetryCount}, MaxRetries: {MaxRetries}",
                    message.MessageId,
                    message.IntegrationType,
                    message.OrganizationId,
                    result.Category,
                    result.FailureReason,
                    message.RetryCount,
                    _maxRetries);
                return false;
            }
        }
        catch (Exception ex)
        {
            // Unknown exception - log error, return true so the message will be acknowledged and not resent
            _logger.LogError(ex, "Unhandled error processing ASB message");
            return true;
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var json = args.Message.Body.ToString();
        if (await HandleMessageAsync(json))
        {
            await args.CompleteMessageAsync(args.Message);
        }
        else
        {
            await args.DeadLetterMessageAsync(args.Message, "Retry limit exceeded or non-retryable");
        }
    }
}
