using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;
using Event = Bit.Core.Entities.Event;
using IEventRepository = Bit.Core.Repositories.IEventRepository;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

/// <summary>
/// Covers <c>DeleteManyByOrganizationIdAsync</c> — the purge the whole delete-task queue exists to
/// drive. <c>IEventRepository</c> is only registered for self-hosted deployments, hence
/// <c>SelfHosted = true</c>; that resolves to the Dapper implementation on SQL Server and the EF
/// implementation on the other providers, so both SQL-backed purge paths are exercised. Cloud's
/// Table Storage implementation is not reachable from this harness.
/// </summary>
public class EventRepositoryDeleteManyTests
{
    [Theory, DatabaseData(SelfHosted = true)]
    public async Task DeleteManyByOrganizationIdAsync_DeletesOnlyTheGivenOrganizationsEvents(
        IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();
        var otherOrganizationId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(organizationId, 3));
        await sut.CreateManyAsync(BuildEvents(otherOrganizationId, 2));

        var deleted = await sut.DeleteManyByOrganizationIdAsync(organizationId, TestContext.Current.CancellationToken);

        Assert.Equal(3, deleted);
        Assert.Empty(await ReadEventsAsync(sut, organizationId));
        // A purge scoped to the wrong rows would be a data-loss bug, so assert the bystander too.
        Assert.Equal(2, (await ReadEventsAsync(sut, otherOrganizationId)).Count);
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task DeleteManyByOrganizationIdAsync_ReturnsZeroWhenNothingRemains(
        IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();
        await sut.CreateManyAsync(BuildEvents(organizationId, 2));

        await sut.DeleteManyByOrganizationIdAsync(organizationId, TestContext.Current.CancellationToken);
        var second = await sut.DeleteManyByOrganizationIdAsync(organizationId, TestContext.Current.CancellationToken);

        // The job loops until this returns 0. If it never did, a completed cleanup would spin until
        // the run budget expired and the task would never be marked complete.
        Assert.Equal(0, second);
    }

    [Theory, DatabaseData(SelfHosted = true)]
    public async Task DeleteManyByOrganizationIdAsync_NoEvents_ReturnsZero(IEventRepository sut)
    {
        Assert.Equal(0, await sut.DeleteManyByOrganizationIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
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

    private static async Task<List<IEvent>> ReadEventsAsync(IEventRepository sut, Guid organizationId)
    {
        var result = await sut.GetManyByOrganizationAsync(
            organizationId,
            DateTime.UtcNow.AddDays(-1),
            DateTime.UtcNow.AddDays(1),
            new PageOptions { PageSize = 100 });
        return result.Data.ToList();
    }
}
