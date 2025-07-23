#nullable enable

using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Services;

public class RabbitMqEventListenerService : EventLoggingListenerService
{
    private readonly Lazy<Task<IChannel>> _lazyChannel;
    private readonly string _queueName;
    private readonly IRabbitMqService _rabbitMqService;

    public RabbitMqEventListenerService(
        IEventMessageHandler handler,
        string queueName,
        IRabbitMqService rabbitMqService,
        ILogger<RabbitMqEventListenerService> logger) : base(handler, logger)
    {
        _logger = logger;
        _queueName = queueName;
        _rabbitMqService = rabbitMqService;
        _lazyChannel = new Lazy<Task<IChannel>>(() => _rabbitMqService.CreateChannelAsync());
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await _rabbitMqService.CreateEventQueueAsync(_queueName, cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var channel = await _lazyChannel.Value;
        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) => { await ProcessReceivedMessageAsync(eventArgs); };

        await channel.BasicConsumeAsync(_queueName, autoAck: true, consumer: consumer, cancellationToken: cancellationToken);
    }

    internal async Task ProcessReceivedMessageAsync(BasicDeliverEventArgs eventArgs)
    {
        await ProcessReceivedMessageAsync(
            Encoding.UTF8.GetString(eventArgs.Body.Span),
            eventArgs.BasicProperties.MessageId);
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
