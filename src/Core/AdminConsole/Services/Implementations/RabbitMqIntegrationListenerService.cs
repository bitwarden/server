using System.Text;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public class RabbitMqIntegrationListenerService : BackgroundService
{
    private const string _deadLetterRoutingKey = "dead-letter";
    private IChannel _channel;
    private IConnection _connection;
    private readonly string _exchangeName;
    private readonly string _queueName;
    private readonly string _retryQueueName;
    private readonly string _deadLetterQueueName;
    private readonly string _routingKey;
    private readonly string _retryRoutingKey;
    private readonly int _maxRetries;
    private readonly IIntegrationHandler _handler;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqIntegrationListenerService> _logger;
    private readonly int _retryTiming;

    public RabbitMqIntegrationListenerService(IIntegrationHandler handler,
        string routingKey,
        string queueName,
        string retryQueueName,
        string deadLetterQueueName,
        GlobalSettings globalSettings,
        ILogger<RabbitMqIntegrationListenerService> logger)
    {
        _handler = handler;
        _routingKey = routingKey;
        _retryRoutingKey = $"{_routingKey}-retry";
        _queueName = queueName;
        _retryQueueName = retryQueueName;
        _deadLetterQueueName = deadLetterQueueName;
        _logger = logger;
        _exchangeName = globalSettings.EventLogging.RabbitMq.IntegrationExchangeName;
        _maxRetries = globalSettings.EventLogging.RabbitMq.MaxRetries;
        _retryTiming = globalSettings.EventLogging.RabbitMq.RetryTiming;

        _factory = new ConnectionFactory
        {
            HostName = globalSettings.EventLogging.RabbitMq.HostName,
            UserName = globalSettings.EventLogging.RabbitMq.Username,
            Password = globalSettings.EventLogging.RabbitMq.Password
        };
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(exchange: _exchangeName,
                                            type: ExchangeType.Direct,
                                            durable: true,
                                            cancellationToken: cancellationToken);

        // Declare main queue
        await _channel.QueueDeclareAsync(queue: _queueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(queue: _queueName,
                                      exchange: _exchangeName,
                                      routingKey: _routingKey,
                                      cancellationToken: cancellationToken);

        // Declare retry queue (Configurable TTL, dead-letters back to main queue)
        await _channel.QueueDeclareAsync(queue: _retryQueueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: new Dictionary<string, object>
                                         {
                                             { "x-dead-letter-exchange", _exchangeName },
                                             { "x-dead-letter-routing-key", _routingKey },
                                             { "x-message-ttl", _retryTiming }
                                         },
                                         cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(queue: _retryQueueName,
                                      exchange: _exchangeName,
                                      routingKey: _retryRoutingKey,
                                      cancellationToken: cancellationToken);

        // Declare dead letter queue
        await _channel.QueueDeclareAsync(queue: _deadLetterQueueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(queue: _deadLetterQueueName,
                                      exchange: _exchangeName,
                                      routingKey: _deadLetterRoutingKey,
                                      cancellationToken: cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);

            try
            {
                var result = await _handler.HandleAsync(json);
                var message = result.Message;

                if (result.Success)
                {
                    // Successful integration send. Acknowledge message delivery and return.
                    await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
                    return;
                }

                if (result.Retryable)
                {
                    // Integration failed, but is retryable - apply delay and check max retries
                    message.ApplyRetry(result.NotBeforeUtc);

                    if (message.RetryCount < _maxRetries)
                    {
                        // Publish message to the retry queue. It will be re-published for retry after a delay
                        await _channel.BasicPublishAsync(
                            exchange: _exchangeName,
                            routingKey: _retryRoutingKey,
                            body: Encoding.UTF8.GetBytes(message.ToJson()),
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        // Exceeded the max number of retries; fail and send to dead letter queue
                        await PublishToDeadLetterAsync(message.ToJson());
                        _logger.LogWarning("Max retry attempts reached. Sent to DLQ.");
                    }
                }
                else
                {
                    // Fatal error (i.e. not retryable) occurred. Send message to dead letter queue without any retries
                    await PublishToDeadLetterAsync(message.ToJson());
                    _logger.LogWarning("Non-retryable failure. Sent to DLQ.");
                }

                // Message has been sent to retry or dead letter queues.
                // Acknowledge receipt so Rabbit knows it's been processed.
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
            }
            catch (Exception ex)
            {
                // Unknown error occurred. Acknowledge so Rabbit doesn't keep attempting. Log the error.
                _logger.LogError(ex, "Unhandled error processing integration message.");
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken);
            }
        };

        await _channel.BasicConsumeAsync(queue: _queueName, autoAck: false, consumer: consumer, cancellationToken: cancellationToken);
    }

    private async Task PublishToDeadLetterAsync(string json)
    {
        await _channel.BasicPublishAsync(
            exchange: _exchangeName,
            routingKey: _deadLetterRoutingKey,
            body: Encoding.UTF8.GetBytes(json));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _channel.CloseAsync(cancellationToken);
        await _connection.CloseAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
        base.Dispose();
    }
}
