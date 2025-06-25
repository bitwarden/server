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
        var expected = JsonSerializer.Serialize(eventMessage);
        await Subject.CreateAsync(eventMessage);
        await _eventIntegrationPublisher.Received(1).PublishEventAsync(
            Arg.Is<string>(body => AssertJsonStringsMatch(eventMessage, body)));
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_EventsPublishedToEventQueue(IEnumerable<EventMessage> eventMessages)
    {
        await Subject.CreateManyAsync(eventMessages);
        await _eventIntegrationPublisher.Received(1).PublishEventAsync(
            Arg.Is<string>(body => AssertJsonStringsMatch(eventMessages, body)));
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
