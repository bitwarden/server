#nullable enable

using System.Text;
using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public class RabbitMqIntegrationListenerService<TConfiguration> : BackgroundService
    where TConfiguration : IIntegrationListenerConfiguration
{
    private readonly int _maxRetries;
    private readonly string _queueName;
    private readonly string _routingKey;
    private readonly string _retryQueueName;
    private readonly IIntegrationHandler _handler;
    private readonly Lazy<Task<IChannel>> _lazyChannel;
    private readonly IRabbitMqService _rabbitMqService;
    private readonly ILogger _logger;
    private readonly TimeProvider _timeProvider;

    public RabbitMqIntegrationListenerService(
        IIntegrationHandler handler,
        TConfiguration configuration,
        IRabbitMqService rabbitMqService,
        ILoggerFactory loggerFactory,
        TimeProvider timeProvider)
    {
        _handler = handler;
        _maxRetries = configuration.MaxRetries;
        _routingKey = configuration.RoutingKey;
        _retryQueueName = configuration.IntegrationRetryQueueName;
        _queueName = configuration.IntegrationQueueName;
        _rabbitMqService = rabbitMqService;
        _timeProvider = timeProvider;
        _lazyChannel = new Lazy<Task<IChannel>>(() => _rabbitMqService.CreateChannelAsync());
        _logger = loggerFactory.CreateLogger(
            categoryName: $"Bit.Core.Services.RabbitMqIntegrationListenerService.{configuration.IntegrationQueueName}"); ;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _rabbitMqService.CreateIntegrationQueuesAsync(
            _queueName,
            _retryQueueName,
            _routingKey,
            cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var channel = await _lazyChannel.Value;
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            await ProcessReceivedMessageAsync(ea, cancellationToken);
        };

        await channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    internal async Task ProcessReceivedMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        var channel = await _lazyChannel.Value;
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);

            // Determine if the message came off of the retry queue too soon
            // If so, place it back on the retry queue
            var integrationMessage = JsonSerializer.Deserialize<IntegrationMessage>(json);
            if (integrationMessage is not null &&
                integrationMessage.DelayUntilDate.HasValue &&
                integrationMessage.DelayUntilDate.Value > _timeProvider.GetUtcNow().UtcDateTime)
            {
                await _rabbitMqService.RepublishToRetryQueueAsync(channel, ea);
                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                return;
            }

            var result = await _handler.HandleAsync(json);
            var message = result.Message;

            if (result.Success)
            {
                // Successful integration send. Acknowledge message delivery and return
                await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                return;
            }

            if (result.Retryable)
            {
                // Integration failed, but is retryable - apply delay and check max retries
                message.ApplyRetry(result.DelayUntilDate);

                if (message.RetryCount < _maxRetries)
                {
                    // Publish message to the retry queue. It will be re-published for retry after a delay
                    await _rabbitMqService.PublishToRetryAsync(channel, message, cancellationToken);
                }
                else
                {
                    // Exceeded the max number of retries; fail and send to dead letter queue
                    await _rabbitMqService.PublishToDeadLetterAsync(channel, message, cancellationToken);
                    _logger.LogWarning("Max retry attempts reached. Sent to DLQ.");
                }
            }
            else
            {
                // Fatal error (i.e. not retryable) occurred. Send message to dead letter queue without any retries
                await _rabbitMqService.PublishToDeadLetterAsync(channel, message, cancellationToken);
                _logger.LogWarning("Non-retryable failure. Sent to DLQ.");
            }

            // Message has been sent to retry or dead letter queues.
            // Acknowledge receipt so Rabbit knows it's been processed
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            // Unknown error occurred. Acknowledge so Rabbit doesn't keep attempting. Log the error
            _logger.LogError(ex, "Unhandled error processing integration message.");
            await channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_lazyChannel.IsValueCreated)
        {
            var channel = await _lazyChannel.Value;
            await channel.CloseAsync(cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        if (_lazyChannel.IsValueCreated && _lazyChannel.Value.IsCompletedSuccessfully)
        {
            _lazyChannel.Value.Result.Dispose();
        }
        base.Dispose();
    }
}
