using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EventRouteServiceTests
{
    private readonly IEventWriteService _broadcastEventWriteService = Substitute.For<IEventWriteService>();
    private readonly IEventWriteService _storageEventWriteService = Substitute.For<IEventWriteService>();
    private readonly IFeatureService _featureService = Substitute.For<IFeatureService>();
    private readonly EventRouteService Subject;

    public EventRouteServiceTests()
    {
        Subject = new EventRouteService(_broadcastEventWriteService, _storageEventWriteService, _featureService);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_FlagDisabled_EventSentToStorageService(EventMessage eventMessage)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations).Returns(false);

        await Subject.CreateAsync(eventMessage);

        await _broadcastEventWriteService.DidNotReceiveWithAnyArgs().CreateAsync(Arg.Any<EventMessage>());
        await _storageEventWriteService.Received(1).CreateAsync(eventMessage);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_FlagEnabled_EventSentToBroadcastService(EventMessage eventMessage)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations).Returns(true);

        await Subject.CreateAsync(eventMessage);

        await _broadcastEventWriteService.Received(1).CreateAsync(eventMessage);
        await _storageEventWriteService.DidNotReceiveWithAnyArgs().CreateAsync(Arg.Any<EventMessage>());
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_FlagDisabled_EventsSentToStorageService(IEnumerable<EventMessage> eventMessages)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations).Returns(false);

        await Subject.CreateManyAsync(eventMessages);

        await _broadcastEventWriteService.DidNotReceiveWithAnyArgs().CreateManyAsync(Arg.Any<IEnumerable<EventMessage>>());
        await _storageEventWriteService.Received(1).CreateManyAsync(eventMessages);
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_FlagEnabled_EventsSentToBroadcastService(IEnumerable<EventMessage> eventMessages)
    {
        _featureService.IsEnabled(FeatureFlagKeys.EventBasedOrganizationIntegrations).Returns(true);

        await Subject.CreateManyAsync(eventMessages);

        await _broadcastEventWriteService.Received(1).CreateManyAsync(eventMessages);
        await _storageEventWriteService.DidNotReceiveWithAnyArgs().CreateManyAsync(Arg.Any<IEnumerable<EventMessage>>());
    }
}
