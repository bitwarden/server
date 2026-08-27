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
        // The window is the same one the approver-side history reads use; that agreement is the point (PM-42614), so
        // the expectation is derived from the shared constant rather than restating 90 days here.
        var expectedSince = _now.AddDays(-AccessHistoryWindow.RetentionDays);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId, expectedSince, _now).Returns([row]);

        var result = await sutProvider.Sut.GetMineAsync(userId, _now);

        Assert.Single(result);
        // `now` is passed alongside `since` because it does two further jobs the window bound does not: it decides
        // which approved requests still have an unlapsed window (and so survive the window), and it is the clock each
        // row's produced-lease status is projected against (PM-42355).
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
        // The live-row exemption belongs to the read, not to this query: the query hands down one `since` and never
        // filters the rows it gets back. Asserting that here pins the split, so a later "helpful" post-filter on the
        // returned collection cannot quietly reintroduce the bug for pending requests older than the window.
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
