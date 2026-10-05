using Bit.Core.Exceptions;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class ListAccessAuditItemsQueryTests
{
    private static readonly DateTime _now = new(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime RetentionFloor => _now.AddDays(-AccessHistoryWindow.RetentionDays);

    [Theory, BitAutoData]
    public async Task GetItemsAsync_WithNoBounds_ReadsTheWholeRetentionWindow(Guid organizationId)
    {
        var (sutProvider, ranges) = Setup(organizationId);

        await sutProvider.Sut.GetItemsAsync(organizationId, null, null);

        Assert.Equal((RetentionFloor, _now), Assert.Single(ranges));
    }

    [Theory, BitAutoData]
    public async Task GetItemsAsync_WithBounds_PassesThemThrough(Guid organizationId)
    {
        var (sutProvider, ranges) = Setup(organizationId);
        var start = _now.AddDays(-7);
        var end = _now.AddDays(-1);

        await sutProvider.Sut.GetItemsAsync(organizationId, start, end);

        Assert.Equal((start, end), Assert.Single(ranges));
    }

    // Resolved through AccessHistoryWindow, as the trail read is.
    [Theory, BitAutoData]
    public async Task GetItemsAsync_StartBeyondRetention_IsClampedToTheWindow(Guid organizationId)
    {
        var (sutProvider, ranges) = Setup(organizationId);

        await sutProvider.Sut.GetItemsAsync(
            organizationId,
            _now.AddDays(-AccessHistoryWindow.RetentionDays - 10),
            _now.AddDays(-AccessHistoryWindow.RetentionDays + 5));

        Assert.Equal(RetentionFloor, Assert.Single(ranges).Since);
    }

    [Theory, BitAutoData]
    public async Task GetItemsAsync_RangeWiderThanRetention_ThrowsBadRequest(Guid organizationId)
    {
        var (sutProvider, _) = Setup(organizationId);

        await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.GetItemsAsync(
            organizationId, _now.AddDays(-AccessHistoryWindow.RetentionDays - 1), _now));
    }

    [Theory, BitAutoData]
    public async Task GetItemsAsync_ReturnsEverySubjectTheStoreNames(Guid organizationId, Guid cipherId, Guid ruleId)
    {
        var (sutProvider, _) = Setup(organizationId,
            new AccessAuditItem { CipherId = cipherId },
            new AccessAuditItem { RuleId = ruleId, RuleName = "Production database" });

        var items = await sutProvider.Sut.GetItemsAsync(organizationId, null, null);

        Assert.Collection(items,
            item => Assert.Equal(cipherId, item.CipherId),
            item => Assert.Equal(ruleId, item.RuleId));
    }

    private static (SutProvider<ListAccessAuditItemsQuery> SutProvider, List<(DateTime Since, DateTime Until)> Ranges)
        Setup(Guid organizationId, params AccessAuditItem[] items)
    {
        var sutProvider = new SutProvider<ListAccessAuditItemsQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);

        var ranges = new List<(DateTime, DateTime)>();
        sutProvider.GetDependency<IAccessAuditEventRepository>()
            .GetItemsByOrganizationIdAsync(organizationId, Arg.Any<DateTime>(), Arg.Any<DateTime>())
            .Returns(call =>
            {
                ranges.Add((call.ArgAt<DateTime>(1), call.ArgAt<DateTime>(2)));
                return items;
            });

        return (sutProvider, ranges);
    }
}
