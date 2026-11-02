using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class ListMyAccessRequestsQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetMineAsync_QueriesWithSharedRetentionWindow(Guid userId, AccessRequestDetails row)
    {
        var sutProvider = Setup();
        // Shares the same window the approver-side history reads use.
        var expectedSince = _now.AddDays(-AccessHistoryWindow.RetentionDays);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId, expectedSince, _now).Returns([row]);

        var result = await sutProvider.Sut.GetMineAsync(userId, _now);

        Assert.Single(result);
        // `now` also decides unlapsed windows and is the clock lease status is projected against.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .GetManyByRequesterIdAsync(userId, expectedSince, _now);
    }

    [Theory, BitAutoData]
    public async Task GetMineAsync_NoRows_ReturnsEmpty(Guid userId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId, Arg.Any<DateTime?>(), Arg.Any<DateTime>()).Returns([]);

        Assert.Empty(await sutProvider.Sut.GetMineAsync(userId, _now));
    }

    [Theory, BitAutoData]
    public async Task GetMineAsync_DoesNotWindowAwayLiveRows_LeavingThatToTheRead(Guid userId)
    {
        // The live-row exemption belongs to the read; this query hands down one `since` and never post-filters.
        var sutProvider = Setup();
        var aged = new AccessRequestDetails
        {
            Id = Guid.NewGuid(),
            CreationDate = _now.AddDays(-AccessHistoryWindow.RetentionDays - 30),
        };
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId, Arg.Any<DateTime?>(), Arg.Any<DateTime>()).Returns([aged]);

        Assert.Equal(aged.Id, Assert.Single(await sutProvider.Sut.GetMineAsync(userId, _now)).Id);
    }

    private static SutProvider<ListMyAccessRequestsQuery> Setup()
    {
        var sutProvider = new SutProvider<ListMyAccessRequestsQuery>().Create();
        return sutProvider;
    }
}
