using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public abstract class RabbitMqEventListenerBase : BackgroundService
{
    private IChannel _channel;
    private IConnection _connection;
    private readonly string _exchangeName;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<RabbitMqEventListenerBase> _logger;

    protected abstract string QueueName { get; }

    protected RabbitMqEventListenerBase(
        ILogger<RabbitMqEventListenerBase> logger,
        GlobalSettings globalSettings)
    {
        _factory = new ConnectionFactory
        {
            HostName = globalSettings.RabbitMq.HostName,
            UserName = globalSettings.RabbitMq.Username,
            Password = globalSettings.RabbitMq.Password
        };
        _exchangeName = globalSettings.RabbitMq.ExchangeName;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _connection = await _factory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await _channel.ExchangeDeclareAsync(exchange: _exchangeName, type: ExchangeType.Fanout);
        await _channel.QueueDeclareAsync(queue: QueueName,
                                         durable: false,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: cancellationToken);
        await _channel.QueueBindAsync(queue: QueueName, exchange: _exchangeName, routingKey: "");
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, eventArgs) =>
        {
            try
            {
                var eventMessage = JsonSerializer.Deserialize<EventMessage>(eventArgs.Body.Span);
                await HandleMessageAsync(eventMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing the message");
            }
        };

        await _channel.BasicConsumeAsync(QueueName, autoAck: true, consumer: consumer, cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1_000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _channel.CloseAsync();
        await _connection.CloseAsync();
        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
        base.Dispose();
    }

    protected abstract Task HandleMessageAsync(EventMessage eventMessage);
}
