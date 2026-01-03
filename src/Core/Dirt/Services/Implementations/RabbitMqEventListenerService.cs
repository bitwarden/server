using System.Text;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Bit.Core.Dirt.Services.Implementations;

public class RabbitMqEventListenerService<TConfiguration> : EventLoggingListenerService
    where TConfiguration : IEventListenerConfiguration
{
    private readonly Lazy<Task<IChannel>> _lazyChannel;
    private readonly string _queueName;
    private readonly IRabbitMqService _rabbitMqService;

    public RabbitMqEventListenerService(
        IEventMessageHandler handler,
        TConfiguration configuration,
        IRabbitMqService rabbitMqService,
        ILoggerFactory loggerFactory)
        : base(handler, CreateLogger(loggerFactory, configuration))
    {
        _queueName = configuration.EventQueueName;
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

    private static ILogger CreateLogger(ILoggerFactory loggerFactory, TConfiguration configuration)
    {
        return loggerFactory.CreateLogger(
            categoryName: $"Bit.Core.Dirt.Services.Implementations.RabbitMqEventListenerService.{configuration.EventQueueName}");
    }
}
