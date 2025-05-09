using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class SlackEventHandlerTests
{
    private readonly IOrganizationIntegrationConfigurationRepository _repository = Substitute.For<IOrganizationIntegrationConfigurationRepository>();
    private readonly ISlackService _slackService = Substitute.For<ISlackService>();
    private readonly string _channelId = "C12345";
    private readonly string _channelId2 = "C67890";
    private readonly string _token = "xoxb-test-token";
    private readonly string _token2 = "xoxb-another-test-token";

    private SutProvider<SlackEventHandler> GetSutProvider(
        List<OrganizationIntegrationConfigurationDetails> integrationConfigurations)
    {
        _repository.GetConfigurationDetailsAsync(Arg.Any<Guid>(),
            IntegrationType.Slack, Arg.Any<EventType>())
        .Returns(integrationConfigurations);

        return new SutProvider<SlackEventHandler>()
            .SetDependency(_repository)
            .SetDependency(_slackService)
            .Create();
    }

    private List<OrganizationIntegrationConfigurationDetails> NoConfigurations()
    {
        return [];
    }

    private List<OrganizationIntegrationConfigurationDetails> OneConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = JsonSerializer.Serialize(new { token = _token });
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { channelId = _channelId });
        config.Template = "Date: #Date#, Type: #Type#, UserId: #UserId#";

        return [config];
    }

    private List<OrganizationIntegrationConfigurationDetails> TwoConfigurations()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = JsonSerializer.Serialize(new { token = _token });
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { channelId = _channelId });
        config.Template = "Date: #Date#, Type: #Type#, UserId: #UserId#";
        var config2 = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config2.Configuration = JsonSerializer.Serialize(new { token = _token2 });
        config2.IntegrationConfiguration = JsonSerializer.Serialize(new { channelId = _channelId2 });
        config2.Template = "Date: #Date#, Type: #Type#, UserId: #UserId#";

        return [config, config2];
    }

    private List<OrganizationIntegrationConfigurationDetails> WrongConfiguration()
    {
        var config = Substitute.For<OrganizationIntegrationConfigurationDetails>();
        config.Configuration = JsonSerializer.Serialize(new { });
        config.IntegrationConfiguration = JsonSerializer.Serialize(new { });
        config.Template = "Date: #Date#, Type: #Type#, UserId: #UserId#";

        return [config];
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_NoConfigurations_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(NoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<ISlackService>().DidNotReceiveWithAnyArgs();
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_OneConfiguration_SendsEventViaSlackService(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(OneConfiguration());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}")),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_TwoConfigurations_SendsMultipleEvents(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(TwoConfigurations());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token)),
            Arg.Is(AssertHelper.AssertPropertyEqual(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}")),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId))
        );
        await sutProvider.GetDependency<ISlackService>().Received(1).SendSlackMessageByChannelIdAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual(_token2)),
            Arg.Is(AssertHelper.AssertPropertyEqual(
                $"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}")),
            Arg.Is(AssertHelper.AssertPropertyEqual(_channelId2))
        );
    }

    [Theory, BitAutoData]
    public async Task HandleEventAsync_WrongConfiguration_DoesNothing(EventMessage eventMessage)
    {
        var sutProvider = GetSutProvider(WrongConfiguration());

        await sutProvider.Sut.HandleEventAsync(eventMessage);
        sutProvider.GetDependency<ISlackService>().DidNotReceiveWithAnyArgs();
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_OneConfiguration_SendsEventsViaSlackService(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(OneConfiguration());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        var received = sutProvider.GetDependency<ISlackService>().ReceivedCalls();
        using var calls = received.GetEnumerator();

        Assert.Equal(eventMessages.Count, received.Count());

        foreach (var eventMessage in eventMessages)
        {
            Assert.True(calls.MoveNext());
            var arguments = calls.Current.GetArguments();
            Assert.Equal(_token, arguments[0] as string);
            Assert.Equal($"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}",
                arguments[1] as string);
            Assert.Equal(_channelId, arguments[2] as string);
        }
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventsAsync_TwoConfigurations_SendsMultipleEvents(List<EventMessage> eventMessages)
    {
        var sutProvider = GetSutProvider(TwoConfigurations());

        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);

        var received = sutProvider.GetDependency<ISlackService>().ReceivedCalls();
        using var calls = received.GetEnumerator();

        Assert.Equal(eventMessages.Count * 2, received.Count());

        foreach (var eventMessage in eventMessages)
        {
            Assert.True(calls.MoveNext());
            var arguments = calls.Current.GetArguments();
            Assert.Equal(_token, arguments[0] as string);
            Assert.Equal($"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}",
                         arguments[1] as string);
            Assert.Equal(_channelId, arguments[2] as string);

            Assert.True(calls.MoveNext());
            var arguments2 = calls.Current.GetArguments();
            Assert.Equal(_token2, arguments2[0] as string);
            Assert.Equal($"Date: {eventMessage.Date}, Type: {eventMessage.Type}, UserId: {eventMessage.UserId}",
                arguments2[1] as string);
            Assert.Equal(_channelId2, arguments2[2] as string);
        }
    }
}
