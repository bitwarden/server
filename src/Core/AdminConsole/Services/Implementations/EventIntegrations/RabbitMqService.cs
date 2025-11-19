using System.Text;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Enums;
using Bit.Core.Settings;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public class RabbitMqService : IRabbitMqService
{
    private const string _deadLetterRoutingKey = "dead-letter";

    private readonly ConnectionFactory _factory;
    private readonly Lazy<Task<IConnection>> _lazyConnection;
    private readonly string _deadLetterQueueName;
    private readonly string _eventExchangeName;
    private readonly string _integrationExchangeName;
    private readonly int _retryTiming;
    private readonly bool _useDelayPlugin;

    public RabbitMqService(GlobalSettings globalSettings)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.EventLogging.RabbitMq.HostName,
            UserName = globalSettings.EventLogging.RabbitMq.Username,
            Password = globalSettings.EventLogging.RabbitMq.Password
        };
        _deadLetterQueueName = globalSettings.EventLogging.RabbitMq.IntegrationDeadLetterQueueName;
        _eventExchangeName = globalSettings.EventLogging.RabbitMq.EventExchangeName;
        _integrationExchangeName = globalSettings.EventLogging.RabbitMq.IntegrationExchangeName;
        _retryTiming = globalSettings.EventLogging.RabbitMq.RetryTiming;
        _useDelayPlugin = globalSettings.EventLogging.RabbitMq.UseDelayPlugin;

        _lazyConnection = new Lazy<Task<IConnection>>(CreateConnectionAsync);
    }

    public async Task<IChannel> CreateChannelAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _lazyConnection.Value;
        return await connection.CreateChannelAsync(cancellationToken: cancellationToken);
    }

    public async Task CreateEventQueueAsync(string queueName, CancellationToken cancellationToken = default)
    {
        using var channel = await CreateChannelAsync(cancellationToken);
        await channel.QueueDeclareAsync(queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue: queueName,
            exchange: _eventExchangeName,
            routingKey: string.Empty,
            cancellationToken: cancellationToken);
    }

    public async Task CreateIntegrationQueuesAsync(
        string queueName,
        string retryQueueName,
        string routingKey,
        CancellationToken cancellationToken = default)
    {
        using var channel = await CreateChannelAsync(cancellationToken);
        var retryRoutingKey = $"{routingKey}-retry";

        // Declare main integration queue
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
        await channel.QueueBindAsync(
            queue: queueName,
            exchange: _integrationExchangeName,
            routingKey: routingKey,
            cancellationToken: cancellationToken);

        if (!_useDelayPlugin)
        {
            // Declare retry queue (Configurable TTL, dead-letters back to main queue)
            // Only needed if NOT using delay plugin
            await channel.QueueDeclareAsync(queue: retryQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", _integrationExchangeName },
                    { "x-dead-letter-routing-key", routingKey },
                    { "x-message-ttl", _retryTiming }
                },
                cancellationToken: cancellationToken);
            await channel.QueueBindAsync(queue: retryQueueName,
                exchange: _integrationExchangeName,
                routingKey: retryRoutingKey,
                cancellationToken: cancellationToken);
        }
    }

    public async Task PublishAsync(IIntegrationMessage message)
    {
        var routingKey = message.IntegrationType.ToRoutingKey();
        await using var channel = await CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(message.ToJson());
        var properties = new BasicProperties
        {
            MessageId = message.MessageId,
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: _integrationExchangeName,
            mandatory: true,
            basicProperties: properties,
            routingKey: routingKey,
            body: body);
    }

    public async Task PublishEventAsync(string body, string? organizationId)
    {
        await using var channel = await CreateChannelAsync();
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: _eventExchangeName,
            mandatory: true,
            basicProperties: properties,
            routingKey: string.Empty,
            body: Encoding.UTF8.GetBytes(body));
    }

    public async Task PublishToRetryAsync(IChannel channel, IIntegrationMessage message, CancellationToken cancellationToken)
    {
        var routingKey = message.IntegrationType.ToRoutingKey();
        var retryRoutingKey = $"{routingKey}-retry";
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = message.MessageId,
            Headers = _useDelayPlugin && message.DelayUntilDate.HasValue ?
                new Dictionary<string, object?>
                {
                    ["x-delay"] = Math.Max((int)(message.DelayUntilDate.Value - DateTime.UtcNow).TotalMilliseconds, 0)
                } :
                null
        };

        await channel.BasicPublishAsync(
            exchange: _integrationExchangeName,
            routingKey: _useDelayPlugin ? routingKey : retryRoutingKey,
            mandatory: true,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(message.ToJson()),
            cancellationToken: cancellationToken);
    }

    public async Task PublishToDeadLetterAsync(
        IChannel channel,
        IIntegrationMessage message,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            MessageId = message.MessageId,
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: _integrationExchangeName,
            mandatory: true,
            basicProperties: properties,
            routingKey: _deadLetterRoutingKey,
            body: Encoding.UTF8.GetBytes(message.ToJson()),
            cancellationToken: cancellationToken);
    }

    public async Task RepublishToRetryQueueAsync(IChannel channel, BasicDeliverEventArgs eventArgs)
    {
        await channel.BasicPublishAsync(
            exchange: _integrationExchangeName,
            routingKey: eventArgs.RoutingKey,
            mandatory: true,
            basicProperties: new BasicProperties(eventArgs.BasicProperties),
            body: eventArgs.Body);
    }

    public async ValueTask DisposeAsync()
    {
        if (_lazyConnection.IsValueCreated)
        {
            var connection = await _lazyConnection.Value;
            await connection.DisposeAsync();
        }
    }

    private async Task<IConnection> CreateConnectionAsync()
    {
        var connection = await _factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        // Declare Exchanges
        await channel.ExchangeDeclareAsync(exchange: _eventExchangeName, type: ExchangeType.Fanout, durable: true);
        if (_useDelayPlugin)
        {
            await channel.ExchangeDeclareAsync(
                exchange: _integrationExchangeName,
                type: "x-delayed-message",
                durable: true,
                arguments: new Dictionary<string, object?>
                {
                    { "x-delayed-type", "direct" }
                }
            );
        }
        else
        {
            await channel.ExchangeDeclareAsync(exchange: _integrationExchangeName, type: ExchangeType.Direct, durable: true);
        }

        // Declare dead letter queue for Integration exchange
        await channel.QueueDeclareAsync(queue: _deadLetterQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);
        await channel.QueueBindAsync(queue: _deadLetterQueueName,
            exchange: _integrationExchangeName,
            routingKey: _deadLetterRoutingKey);

        return connection;
    }
}
