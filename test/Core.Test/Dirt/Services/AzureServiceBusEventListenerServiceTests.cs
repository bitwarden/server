#nullable enable

using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Bit.Core.Dirt.Services;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Models.Data;
using Bit.Core.Test.Dirt.Models.Data.EventIntegrations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class AzureServiceBusEventListenerServiceTests
{
    private const string _messageId = "messageId";
    private readonly TestListenerConfiguration _config = new();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    private SutProvider<AzureServiceBusEventListenerService<TestListenerConfiguration>> GetSutProvider()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<object>().ReturnsForAnyArgs(_logger);
        return new SutProvider<AzureServiceBusEventListenerService<TestListenerConfiguration>>()
            .SetDependency(_config)
            .SetDependency(loggerFactory)
            .Create();
    }

    [Fact]
    public void Constructor_CreatesLogWithCorrectCategory()
    {
        var sutProvider = GetSutProvider();

        var fullName = typeof(AzureServiceBusEventListenerService<>).FullName ?? "";
        var tickIndex = fullName.IndexOf('`');
        var cleanedName = tickIndex >= 0 ? fullName.Substring(0, tickIndex) : fullName;
        var categoryName = cleanedName + '.' + _config.EventSubscriptionName;

        sutProvider.GetDependency<ILoggerFactory>().Received(1).CreateLogger(categoryName);
    }

    [Fact]
    public void Constructor_CreatesProcessor()
    {
        var sutProvider = GetSutProvider();

        sutProvider.GetDependency<IAzureServiceBusService>().Received(1).CreateProcessor(
            Arg.Is(_config.EventTopicName),
            Arg.Is(_config.EventSubscriptionName),
            Arg.Any<ServiceBusProcessorOptions>()
        );
    }

    [Theory, BitAutoData]
    public async Task ProcessErrorAsync_LogsError(ProcessErrorEventArgs args)
    {
        var sutProvider = GetSutProvider();

        await sutProvider.Sut.ProcessErrorAsync(args);

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ProcessReceivedMessageAsync_EmptyJson_LogsError()
    {
        var sutProvider = GetSutProvider();
        await sutProvider.Sut.ProcessReceivedMessageAsync(string.Empty, _messageId);

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
        await sutProvider.Sut.ProcessReceivedMessageAsync("{ Invalid JSON }", _messageId);

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
        await sutProvider.Sut.ProcessReceivedMessageAsync(
            "{ \"not a valid\", \"list of event messages\" }",
            _messageId
        );

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
        await sutProvider.Sut.ProcessReceivedMessageAsync(
            JsonSerializer.Serialize(DateTime.UtcNow), // wrong object - not EventMessage
            _messageId
        );

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
        await sutProvider.Sut.ProcessReceivedMessageAsync(
            JsonSerializer.Serialize(message),
            _messageId
        );

        await sutProvider.GetDependency<IEventMessageHandler>().Received(1).HandleEventAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(message, new[] { "IdempotencyId" })));
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_ManyEvents_DelegatesToHandler(IEnumerable<EventMessage> messages)
    {
        var sutProvider = GetSutProvider();
        await sutProvider.Sut.ProcessReceivedMessageAsync(
            JsonSerializer.Serialize(messages),
            _messageId
        );

        await sutProvider.GetDependency<IEventMessageHandler>().Received(1).HandleManyEventsAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(messages, new[] { "IdempotencyId" })));
    }
}
