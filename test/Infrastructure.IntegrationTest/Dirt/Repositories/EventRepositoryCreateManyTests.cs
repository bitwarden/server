using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;
using Event = Bit.Core.Entities.Event;
using IEventRepository = Bit.Core.Repositories.IEventRepository;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Covers <c>CreateManyAsync</c> on every configured provider. <c>IEventRepository</c> is only
/// registered for self-hosted deployments, hence <c>SelfHosted = true</c>; that resolves to the
/// Dapper implementation on SQL Server and the EF implementation on the other providers.
/// </summary>
public class EventRepositoryCreateManyTests
{
    [Theory, DatabaseData(SelfHosted = true)]
    public async Task CreateManyAsync_MultipleEvents_PersistsEveryEvent(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();

        await sut.CreateManyAsync(BuildEvents(organizationId, 3));

        Assert.Equal(3, (await ReadEventsAsync(sut, organizationId)).Count);
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task CreateManyAsync_SingleEvent_PersistsThatEvent(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();

        await sut.CreateManyAsync(BuildEvents(organizationId, 1));

        Assert.Single(await ReadEventsAsync(sut, organizationId));
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task CreateManyAsync_NoEvents_DoesNotThrow(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();

        await sut.CreateManyAsync([]);

        Assert.Empty(await ReadEventsAsync(sut, organizationId));
    }

    private static List<Event> BuildEvents(Guid organizationId, int count) =>
        Enumerable.Range(0, count)
            .Select(i => new Event
            {
                Type = EventType.Organization_Updated,
                OrganizationId = organizationId,
                Date = DateTime.UtcNow.AddMinutes(-i),
            })
            .ToList();

    private static async Task<IReadOnlyCollection<IEvent>> ReadEventsAsync(
        IEventRepository sut, Guid organizationId)
    {
        var result = await sut.GetManyByOrganizationAsync(
            organizationId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1),
            new PageOptions { PageSize = 100 });
        return result.Data;
    }
}
