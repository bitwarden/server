using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Xunit;
using Event = Bit.Core.Entities.Event;
using IEventRepository = Bit.Core.Repositories.IEventRepository;

namespace Bit.Infrastructure.IntegrationTest.Dirt.Repositories;

public class EventRepositoryCreateManyTests
{
    [Theory, DatabaseData(SelfHosted = true)]
    public async Task CreateManyAsync_MultipleEvents_PersistsSystemUser(IEventRepository sut)
    {
        var organizationId = Guid.NewGuid();
        var events = Enumerable.Range(0, 2)
            .Select(i => new Event
            {
                Type = EventType.OrganizationUser_InviteReminderSent,
                OrganizationId = organizationId,
                OrganizationUserId = Guid.NewGuid(),
                SystemUser = EventSystemUser.ScheduledJob,
                Date = DateTime.UtcNow.AddMinutes(-i),
            })
            .ToList();

        await sut.CreateManyAsync(events);

        var result = await sut.GetManyByOrganizationAsync(organizationId,
            DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1), new PageOptions { PageSize = 100 });

        Assert.Equal(2, result.Data.Count);
        Assert.All(result.Data, e => Assert.Equal(EventSystemUser.ScheduledJob, e.SystemUser));
    }
}
