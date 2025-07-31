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
    private readonly IIntegrationHandler _handler = Substitute.For<IIntegrationHandler>();
    private readonly IAzureServiceBusService _serviceBusService = Substitute.For<IAzureServiceBusService>();
    private readonly TestListenerConfiguration _config = new();

    private SutProvider<AzureServiceBusIntegrationListenerService<TestListenerConfiguration>> GetSutProvider()
    {
        return new SutProvider<AzureServiceBusIntegrationListenerService<TestListenerConfiguration>>()
            .SetDependency(_config)
            .SetDependency(_handler)
            .SetDependency(_serviceBusService)
            .Create();
    }

    [Fact]
    public void Constructor_CreatesProcessor()
    {
        var sutProvider = GetSutProvider();

        sutProvider.GetDependency<IAzureServiceBusService>().Received(1).CreateProcessor(
            Arg.Is(_config.IntegrationTopicName),
            Arg.Is(_config.IntegrationSubscriptionName),
            Arg.Any<ServiceBusProcessorOptions>()
        );
    }

    [Theory, BitAutoData]
    public async Task ProcessErrorAsync_LogsError(ProcessErrorEventArgs args)
    {
        var sutProvider = GetSutProvider();
        await sutProvider.Sut.ProcessErrorAsync(args);

        sutProvider.GetDependency<ILogger<AzureServiceBusIntegrationListenerService<TestListenerConfiguration>>>().Received(1).Log(
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
        message.RetryCount = _config.MaxRetries;
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
