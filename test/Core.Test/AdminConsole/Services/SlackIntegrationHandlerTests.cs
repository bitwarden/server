using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

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

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }
}
