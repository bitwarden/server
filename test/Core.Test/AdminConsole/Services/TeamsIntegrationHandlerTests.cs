using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using Microsoft.Rest;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class TeamsIntegrationHandlerTests
{
    private readonly ITeamsService _teamsService = Substitute.For<ITeamsService>();
    private readonly string _channelId = "C12345";
    private readonly Uri _serviceUrl = new Uri("http://localhost");

    private SutProvider<TeamsIntegrationHandler> GetSutProvider()
    {
        return new SutProvider<TeamsIntegrationHandler>()
            .SetDependency(_teamsService)
            .Create();
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_SuccessfulRequest_ReturnsSuccess(IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new TeamsIntegrationConfigurationDetails(_channelId, _serviceUrl);

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.True(result.Success);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ITeamsService>().Received(1).SendMessageToChannelAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_serviceUrl)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate))
        );
    }


    [Theory, BitAutoData]
    public async Task HandleAsync_HttpExceptionNonRetryable_ReturnsFalseAndNotRetryable(IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new TeamsIntegrationConfigurationDetails(_channelId, _serviceUrl);

        sutProvider.GetDependency<ITeamsService>()
            .SendMessageToChannelAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new HttpOperationException("Server error")
            {
                Response = new HttpResponseMessageWrapper(
                        new HttpResponseMessage(System.Net.HttpStatusCode.Forbidden),
                        "Forbidden"
                    )
            }
            );
        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.False(result.Retryable);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ITeamsService>().Received(1).SendMessageToChannelAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_serviceUrl)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate))
        );
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_HttpExceptionRetryable_ReturnsFalseAndRetryable(IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new TeamsIntegrationConfigurationDetails(_channelId, _serviceUrl);

        sutProvider.GetDependency<ITeamsService>()
            .SendMessageToChannelAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new HttpOperationException("Server error")
            {
                Response = new HttpResponseMessageWrapper(
                        new HttpResponseMessage(System.Net.HttpStatusCode.TooManyRequests),
                        "Too Many Requests"
                    )
            }
            );

        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable);
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ITeamsService>().Received(1).SendMessageToChannelAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_serviceUrl)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate))
        );
    }

    [Theory, BitAutoData]
    public async Task HandleAsync_UnknownException_ReturnsFalseButRetryable(IntegrationMessage<TeamsIntegrationConfigurationDetails> message)
    {
        var sutProvider = GetSutProvider();
        message.Configuration = new TeamsIntegrationConfigurationDetails(_channelId, _serviceUrl);

        sutProvider.GetDependency<ITeamsService>()
            .SendMessageToChannelAsync(Arg.Any<Uri>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new Exception("Unknown error"));
        var result = await sutProvider.Sut.HandleAsync(message);

        Assert.False(result.Success);
        Assert.True(result.Retryable); // Unknown exceptions are classified as TransientError (retryable)
        Assert.Equal(result.Message, message);

        await sutProvider.GetDependency<ITeamsService>().Received(1).SendMessageToChannelAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_serviceUrl)),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId)),
            Arg.Is(AssertHelper.AssertPropertyEqual(message.RenderedTemplate))
        );
    }
}
