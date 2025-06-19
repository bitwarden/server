using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class RabbitMqEventListenerServiceTests
{
    private const string _queueName = "test_queue";
    private readonly IRabbitMqService _rabbitMqService = Substitute.For<IRabbitMqService>();
    private readonly ILogger<RabbitMqEventListenerService> _logger = Substitute.For<ILogger<RabbitMqEventListenerService>>();

    private SutProvider<RabbitMqEventListenerService> GetSutProvider()
    {
        return new SutProvider<RabbitMqEventListenerService>()
            .SetDependency(_rabbitMqService)
            .SetDependency(_logger)
            .SetDependency(_queueName, "queueName")
            .Create();
    }

    [Fact]
    public async Task StartAsync_CreatesQueue()
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        await _rabbitMqService.Received(1).CreateEventQueueAsync(
            Arg.Is(_queueName),
            Arg.Is(cancellationToken)
        );
    }

    [Fact]
    public async Task ProcessReceivedMessageAsync_EmptyJson_LogsError()
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: new byte[0]);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ProcessReceivedMessageAsync_InvalidJson_LogsError()
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: JsonSerializer.SerializeToUtf8Bytes("{ Inavlid JSON"));

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString().Contains("Invalid JSON")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ProcessReceivedMessageAsync_InvalidJsonArray_LogsError()
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: JsonSerializer.SerializeToUtf8Bytes(new[] { "not a valid", "list of event messages" }));

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Fact]
    public async Task ProcessReceivedMessageAsync_InvalidJsonObject_LogsError()
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: JsonSerializer.SerializeToUtf8Bytes(DateTime.UtcNow));  // wrong object - not EventMessage

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<object, Exception, string>>());
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_SingleEvent_DelegatesToHandler(EventMessage message)
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: JsonSerializer.SerializeToUtf8Bytes(message));

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        await sutProvider.GetDependency<IEventMessageHandler>().Received(1).HandleEventAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(message, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_ManyEvents_DelegatesToHandler(IEnumerable<EventMessage> messages)
    {
        var sutProvider = GetSutProvider();
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: JsonSerializer.SerializeToUtf8Bytes(messages));

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        await sutProvider.GetDependency<IEventMessageHandler>().Received(1).HandleManyEventsAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(messages, new[] { "IdempotencyId" })));
    }
}
