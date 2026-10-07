#nullable enable

using System.Text;
using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Services;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Test.Dirt.Models.Data.EventIntegrations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class RabbitMqIntegrationListenerServiceTests
{
    private readonly DateTime _now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
    private readonly IIntegrationHandler _handler = Substitute.For<IIntegrationHandler>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IRabbitMqService _rabbitMqService = Substitute.For<IRabbitMqService>();
    private readonly TestListenerConfiguration _config = new();

    private SutProvider<RabbitMqIntegrationListenerService<TestListenerConfiguration>> GetSutProvider()
    {
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger<object>().ReturnsForAnyArgs(_logger);
        var sutProvider = new SutProvider<RabbitMqIntegrationListenerService<TestListenerConfiguration>>()
            .SetDependency(_config)
            .SetDependency(_handler)
            .SetDependency(loggerFactory)
            .SetDependency(_rabbitMqService)
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        return sutProvider;
    }

    [Fact]
    public void Constructor_CreatesLogWithCorrectCategory()
    {
        var sutProvider = GetSutProvider();

        var fullName = typeof(RabbitMqIntegrationListenerService<>).FullName ?? "";
        var tickIndex = fullName.IndexOf('`');
        var cleanedName = tickIndex >= 0 ? fullName.Substring(0, tickIndex) : fullName;
        var categoryName = cleanedName + '.' + _config.IntegrationQueueName;

        sutProvider.GetDependency<ILoggerFactory>().Received(1).CreateLogger(categoryName);
    }

    [Fact]
    public async Task StartAsync_CreatesQueues()
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        await sutProvider.GetDependency<IRabbitMqService>().Received(1).CreateIntegrationQueuesAsync(
            Arg.Is(_config.IntegrationQueueName),
            Arg.Is(_config.IntegrationRetryQueueName),
            Arg.Is(((IIntegrationListenerConfiguration)_config).RoutingKey),
            Arg.Is(cancellationToken)
        );
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_FailureNotRetryable_PublishesToDeadLetterQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        message.DelayUntilDate = null;
        message.RetryCount = 0;
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: Encoding.UTF8.GetBytes(message.ToJson())
        );
        var result = IntegrationHandlerResult.Fail(
            message: message,
            category: IntegrationFailureCategory.AuthenticationFailed, // NOT retryable
            failureReason: "403");
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());
        Assert.NotNull(expected);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));

        await _rabbitMqService.Received(1).PublishToDeadLetterAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => (o.ToString() ?? "").Contains("Integration failure - non-retryable.")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(Arg.Any<IChannel>(), Arg.Any<BasicDeliverEventArgs>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_FailureRetryableButTooManyRetries_PublishesToDeadLetterQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        message.DelayUntilDate = null;
        message.RetryCount = _config.MaxRetries;
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: Encoding.UTF8.GetBytes(message.ToJson())
        );
        var result = IntegrationHandlerResult.Fail(
            message: message,
            category: IntegrationFailureCategory.TransientError, // Retryable
            failureReason: "403");
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());
        Assert.NotNull(expected);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        expected.ApplyRetry(result.DelayUntilDate);
        await _rabbitMqService.Received(1).PublishToDeadLetterAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => (o.ToString() ?? "").Contains("Integration failure - max retries exceeded.")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(Arg.Any<IChannel>(), Arg.Any<BasicDeliverEventArgs>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_FailureRetryable_PublishesToRetryQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        message.DelayUntilDate = null;
        message.RetryCount = 0;
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: Encoding.UTF8.GetBytes(message.ToJson())
        );
        var result = IntegrationHandlerResult.Fail(
            message: message,
            category: IntegrationFailureCategory.TransientError, // Retryable
            failureReason: "403");
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());
        Assert.NotNull(expected);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));

        expected.ApplyRetry(result.DelayUntilDate);
        await _rabbitMqService.Received(1).PublishToRetryAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(Arg.Any<IChannel>(), Arg.Any<BasicDeliverEventArgs>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_SuccessfulResult_Succeeds(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        message.DelayUntilDate = null;
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: Encoding.UTF8.GetBytes(message.ToJson())
        );
        var result = IntegrationHandlerResult.Succeed(message);
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(message.ToJson()));

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(Arg.Any<IChannel>(), Arg.Any<BasicDeliverEventArgs>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
    }

    [Theory, BitAutoData]
    public async Task ProcessReceivedMessageAsync_TooEarlyRetry_RepublishesToRetryQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var cancellationToken = CancellationToken.None;
        await sutProvider.Sut.StartAsync(cancellationToken);

        message.DelayUntilDate = _now.AddMinutes(1);
        var eventArgs = new BasicDeliverEventArgs(
            consumerTag: string.Empty,
            deliveryTag: 0,
            redelivered: true,
            exchange: string.Empty,
            routingKey: string.Empty,
            new BasicProperties(),
            body: Encoding.UTF8.GetBytes(message.ToJson())
        );

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _rabbitMqService.Received(1)
            .RepublishToRetryQueueAsync(Arg.Any<IChannel>(), Arg.Any<BasicDeliverEventArgs>());

        await _handler.DidNotReceiveWithAnyArgs().HandleAsync(Arg.Any<string>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(Arg.Any<IChannel>(), Arg.Any<IntegrationMessage>(), Arg.Any<CancellationToken>());
    }
}
