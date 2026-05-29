using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Dirt.Models.Data.Slack;
using Bit.Core.Dirt.Services;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class SlackIntegrationHandlerTests
{
    private readonly ISlackService _slackService = Substitute.For<ISlackService>();
    private readonly string _channelId = "C12345";
    private readonly string _token = "xoxb-test-token";

    private SutProvider<SlackIntegrationHandler> GetSutProvider()
    {
        return new SutProvider<SlackIntegrationHandler>()
            .SetDependency(_slackService)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequest_ReturnsSuccess(IntegrationMessage<SlackIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new SlackIntegrationConfigurationDetails(_channelId, _token);

        _slackService.SendSlackMessageByChannelIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SlackSendMessageResponse() { Ok = true, Channel = _channelId });

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }

    [Theory]
    [InlineData("service_unavailable")]
    [InlineData("ratelimited")]
    [InlineData("rate_limited")]
    [InlineData("internal_error")]
    [InlineData("message_limit_exceeded")]
    public async Task HandleAsync_FailedRetryableRequest_ReturnsFailureWithRetryable(string error)
    {
        var sutProvider = GetSutProvider();
        var message = new IntegrationMessage<SlackIntegrationConfigurationDetails>()
        {
            Configuration = new SlackIntegrationConfigurationDetails(_channelId, _token),
            MessageId = "MessageId",
            RenderedTemplate = "Test Message"
        };

        _slackService.SendSlackMessageByChannelIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SlackSendMessageResponse() { Ok = false, Channel = _channelId, Error = error });

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.NotNull(result.FailureReason);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }

    [Theory]
    [InlineData("access_denied")]
    [InlineData("channel_not_found")]
    [InlineData("token_expired")]
    [InlineData("token_revoked")]
    public async Task HandleAsync_FailedNonRetryableRequest_ReturnsNonRetryableFailure(string error)
    {
        var sutProvider = GetSutProvider();
        var message = new IntegrationMessage<SlackIntegrationConfigurationDetails>()
        {
            Configuration = new SlackIntegrationConfigurationDetails(_channelId, _token),
            MessageId = "MessageId",
            RenderedTemplate = "Test Message"
        };

        _slackService.SendSlackMessageByChannelIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(new SlackSendMessageResponse() { Ok = false, Channel = _channelId, Error = error });

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.NotNull(result.FailureReason);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }

    [Fact]
    public async Task HandleAsync_NullResponse_ReturnsRetryableFailure()
    {
        var sutProvider = GetSutProvider();
        var message = new IntegrationMessage<SlackIntegrationConfigurationDetails>()
        {
            Configuration = new SlackIntegrationConfigurationDetails(_channelId, _token),
            MessageId = "MessageId",
            RenderedTemplate = "Test Message"
        };

        _slackService.SendSlackMessageByChannelIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns((SlackSendMessageResponse?)null);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable); // Null response is classified as TransientError (retryable)
        Assert.Equal("Slack response was null", result.FailureReason);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }
}
