using System.Text;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class RabbitMqIntegrationListenerServiceTests
{
    private readonly DateTime _now = new DateTime(2014, 3, 2, 1, 0, 0, DateTimeKind.Utc);
    private readonly IIntegrationHandler _handler = Substitute.For<IIntegrationHandler>();
    private readonly IRabbitMqService _rabbitMqService = Substitute.For<IRabbitMqService>();
    private readonly TestListenerConfiguration _config = new();

    private SutProvider<RabbitMqIntegrationListenerService<TestListenerConfiguration>> GetSutProvider()
    {
        var sutProvider = new SutProvider<RabbitMqIntegrationListenerService<TestListenerConfiguration>>()
            .SetDependency(_config)
            .SetDependency(_handler)
            .SetDependency(_rabbitMqService)
            .WithFakeTimeProvider()
            .Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        return sutProvider;
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
            Arg.Is(((IntegrationListenerConfiguration)_config).RoutingKey),
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
        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = false;
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));

        await _rabbitMqService.Received(1).PublishToDeadLetterAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(default, default, default);
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
        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = true;
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        expected.ApplyRetry(result.DelayUntilDate);
        await _rabbitMqService.Received(1).PublishToDeadLetterAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(default, default, default);
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
        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = true;
        result.DelayUntilDate = _now.AddMinutes(1);
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson());

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));

        expected.ApplyRetry(result.DelayUntilDate);
        await _rabbitMqService.Received(1).PublishToRetryAsync(
            Arg.Any<IChannel>(),
            Arg.Is(AssertHelper.AssertPropertyEqual(expected, new[] { "DelayUntilDate" })),
            Arg.Any<CancellationToken>());

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(default, default, default);
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
        var result = new IntegrationHandlerResult(true, message);
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        await sutProvider.Sut.ProcessReceivedMessageAsync(eventArgs, cancellationToken);

        await _handler.Received(1).HandleAsync(Arg.Is(message.ToJson()));

        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .RepublishToRetryQueueAsync(default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(default, default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(default, default, default);
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

        await _handler.DidNotReceiveWithAnyArgs().HandleAsync(default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToRetryAsync(default, default, default);
        await _rabbitMqService.DidNotReceiveWithAnyArgs()
            .PublishToDeadLetterAsync(default, default, default);
    }
}
