using System.Text.Json;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Services;

[SutProviderCustomize]
public class EventIntegrationEventWriteServiceTests
{
    private readonly IEventIntegrationPublisher _eventIntegrationPublisher = Substitute.For<IEventIntegrationPublisher>();
    private readonly EventIntegrationEventWriteService Subject;

    public EventIntegrationEventWriteServiceTests()
    {
        Subject = new EventIntegrationEventWriteService(_eventIntegrationPublisher);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_EventPublishedToEventQueue(EventMessage eventMessage)
    {
        await Subject.CreateAsync(eventMessage);
        await _eventIntegrationPublisher.Received(1).PublishEventAsync(
            body: Arg.Is<string>(body => AssertJsonStringsMatch(eventMessage, body)),
            organizationId: Arg.Is<string>(orgId => eventMessage.OrganizationId.ToString().Equals(orgId)));
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_EventsPublishedToEventQueue(IEnumerable<EventMessage> eventMessages)
    {
        var eventMessage = eventMessages.First();
        await Subject.CreateManyAsync(eventMessages);
        await _eventIntegrationPublisher.Received(1).PublishEventAsync(
            body: Arg.Is<string>(body => AssertJsonStringsMatch(eventMessages, body)),
            organizationId: Arg.Is<string>(orgId => eventMessage.OrganizationId.ToString().Equals(orgId)));
    }

    [Fact]
    public async Task CreateManyAsync_EmptyList_DoesNothing()
    {
        await Subject.CreateManyAsync([]);
        await _eventIntegrationPublisher.DidNotReceiveWithAnyArgs().PublishEventAsync(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task DisposeAsync_DisposesEventIntegrationPublisher()
    {
        await Subject.DisposeAsync();
        await _eventIntegrationPublisher.Received(1).DisposeAsync();
    }

    private static bool AssertJsonStringsMatch(EventMessage expected, string body)
    {
        var actual = JsonSerializer.Deserialize<EventMessage>(body);
        AssertHelper.AssertPropertyEqual(expected, actual, new[] { "IdempotencyId" });
        return true;
    }

    private static bool AssertJsonStringsMatch(IEnumerable<EventMessage> expected, string body)
    {
        using var actual = JsonSerializer.Deserialize<IEnumerable<EventMessage>>(body).GetEnumerator();

        foreach (var expectedMessage in expected)
        {
            actual.MoveNext();
            AssertHelper.AssertPropertyEqual(expectedMessage, actual.Current, new[] { "IdempotencyId" });
        }
        return true;
    }
}
