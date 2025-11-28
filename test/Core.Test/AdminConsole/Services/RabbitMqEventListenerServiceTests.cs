#nullable enable

using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
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
    private readonly TestListenerConfiguration _config = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private SutProvider<RabbitMqEventListenerService<TestListenerConfiguration>> GetSutProvider()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<object>().ReturnsForAnyArgs(_logger);
        return new SutProvider<RabbitMqEventListenerService<TestListenerConfiguration>>()
            .SetDependency(_config)
            .SetDependency(loggerFactory)
            .Create();
    }

    [Fact]
    public void Constructor_CreatesLogWithCorrectCategory()
    {
        var sutProvider = GetSutProvider();

        var fullName = typeof(RabbitMqEventListenerService<>).FullName ?? "";
        var tickIndex = fullName.IndexOf('`');
        var cleanedName = tickIndex >= 0 ? fullName.Substring(0, tickIndex) : fullName;
        var categoryName = cleanedName + '.' + _config.EventQueueName;

        sutProvider.GetDependency<ILoggerFactory>().Received(1).CreateLogger(categoryName);
    }

    [Fact]
    public async Task StartAsync_CreatesQueue()
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        await sutProvider.GetDependency<IRabbitMqService>().Received(1).CreateEventQueueAsync(
            Arg.Is(_config.EventQueueName),
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
            body: Array.Empty<byte>());

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<JsonException>(),
            Arg.Any<Func<object, Exception?, string>>());
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
            body: JsonSerializer.SerializeToUtf8Bytes("{ Invalid JSON"));

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => (o.ToString() ?? "").Contains("Invalid JSON")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
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
            Arg.Any<Func<object, Exception?, string>>());
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
            Arg.Any<Func<object, Exception?, string>>());
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
