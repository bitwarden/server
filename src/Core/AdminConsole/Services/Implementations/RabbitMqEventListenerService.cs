using System.Text;
using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public class RabbitMqEventListenerService : EventLoggingListenerService
{
    private IChannel _channel;
    private IConnection _connection;
    private readonly string _exchangeName;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqEventListenerService> _logger;
    private readonly string _queueName;

    public RabbitMqEventListenerService(
        IEventMessageHandler handler,
        ILogger<RabbitMqEventListenerService> logger,
        GlobalSettings globalSettings,
        string queueName) : base(handler)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.EventLogging.RabbitMq.HostName,
            UserName = globalSettings.EventLogging.RabbitMq.Username,
            Password = globalSettings.EventLogging.RabbitMq.Password
        };
        _exchangeName = globalSettings.EventLogging.RabbitMq.ExchangeName;
        _logger = logger;
        _queueName = queueName;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(exchange: _exchangeName,
                                            type: ExchangeType.Fanout,
                                            durable: true,
                                            cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(queue: _queueName,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(queue: _queueName,
                                      exchange: _exchangeName,
                                      routingKey: string.Empty,
                                      cancellationToken: cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            try
            {
                using var jsonDocument = JsonDocument.Parse(Encoding.UTF8.GetString(eventArgs.Body.Span));
                var root = jsonDocument.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    var eventMessages = root.Deserialize<List<EventMessage>>();
                    await _handler.HandleManyEventAsync(eventMessages);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    var eventMessage = root.Deserialize<EventMessage>();
                    await _handler.HandleEventAsync(eventMessage);

                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the message");
            }
        };

        await _channel.BasicConsumeAsync(_queueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken);
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
