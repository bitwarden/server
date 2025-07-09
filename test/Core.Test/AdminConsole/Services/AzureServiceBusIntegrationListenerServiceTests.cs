#nullable enable

using Azure.Messaging.ServiceBus;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class AzureServiceBusIntegrationListenerServiceTests
{
    private const int _maxRetries = 3;
    private const string _topicName = "test_topic";
    private const string _subscriptionName = "test_subscription";
    private readonly IIntegrationHandler _handler = Substitute.For<IIntegrationHandler>();
    private readonly IAzureServiceBusService _serviceBusService = Substitute.For<IAzureServiceBusService>();
    private readonly ILogger<AzureServiceBusIntegrationListenerService> _logger =
        Substitute.For<ILogger<AzureServiceBusIntegrationListenerService>>();

    private SutProvider<AzureServiceBusIntegrationListenerService> GetSutProvider()
    {
        return new SutProvider<AzureServiceBusIntegrationListenerService>()
            .SetDependency(_handler)
            .SetDependency(_serviceBusService)
            .SetDependency(_topicName, "topicName")
            .SetDependency(_subscriptionName, "subscriptionName")
            .SetDependency(_maxRetries, "maxRetries")
            .SetDependency(_logger)
            .Create();
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

    [Theory, BitAutoData]
    public async Task HandleMessageAsync_FailureNotRetryable_PublishesToDeadLetterQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        message.RetryCount = 0;

        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = false;
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = (IntegrationMessage<WebhookIntegrationConfiguration>)IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson())!;

        Assert.False(await sutProvider.Sut.HandleMessageAsync(message.ToJson()));

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));
        await _serviceBusService.DidNotReceiveWithAnyArgs().PublishToRetryAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleMessageAsync_FailureRetryableButTooManyRetries_PublishesToDeadLetterQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        message.RetryCount = _maxRetries;
        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = true;

        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = (IntegrationMessage<WebhookIntegrationConfiguration>)IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson())!;

        Assert.False(await sutProvider.Sut.HandleMessageAsync(message.ToJson()));

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));
        await _serviceBusService.DidNotReceiveWithAnyArgs().PublishToRetryAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task HandleMessageAsync_FailureRetryable_PublishesToRetryQueue(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        message.RetryCount = 0;

        var result = new IntegrationHandlerResult(false, message);
        result.Retryable = true;
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = (IntegrationMessage<WebhookIntegrationConfiguration>)IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson())!;

        Assert.True(await sutProvider.Sut.HandleMessageAsync(message.ToJson()));

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));
        await _serviceBusService.Received(1).PublishToRetryAsync(message);
    }

    [Theory, BitAutoData]
    public async Task HandleMessageAsync_SuccessfulResult_Succeeds(IntegrationMessage<WebhookIntegrationConfiguration> message)
    {
        var sutProvider = GetSutProvider();
        var result = new IntegrationHandlerResult(true, message);
        _handler.HandleAsync(Arg.Any<string>()).Returns(result);

        var expected = (IntegrationMessage<WebhookIntegrationConfiguration>)IntegrationMessage<WebhookIntegrationConfiguration>.FromJson(message.ToJson())!;

        Assert.True(await sutProvider.Sut.HandleMessageAsync(message.ToJson()));

        await _handler.Received(1).HandleAsync(Arg.Is(expected.ToJson()));
        await _serviceBusService.DidNotReceiveWithAnyArgs().PublishToRetryAsync(default!);
    }
}
