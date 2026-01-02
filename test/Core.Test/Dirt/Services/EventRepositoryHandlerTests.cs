using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

[SutProviderCustomize]
public class EventRepositoryHandlerTests
{
    [Theory, BitAutoData]
    public async Task HandleEventAsync_WritesEventToIEventWriteService(
        EventMessage eventMessage,
        SutProvider<EventRepositoryHandler> sutProvider)
    {
        await sutProvider.Sut.HandleEventAsync(eventMessage);
        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(eventMessage))
        );
    }

    [Theory, BitAutoData]
    public async Task HandleManyEventAsync_WritesEventsToIEventWriteService(
        IEnumerable<EventMessage> eventMessages,
        SutProvider<EventRepositoryHandler> sutProvider)
    {
        await sutProvider.Sut.HandleManyEventsAsync(eventMessages);
        await sutProvider.GetDependency<IEventWriteService>().Received(1).CreateManyAsync(
            Arg.Is(AssertHelper.AssertPropertyEqual<IEvent>(eventMessages))
        );
    }
}
