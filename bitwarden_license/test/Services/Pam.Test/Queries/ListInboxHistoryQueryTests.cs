using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class ListInboxHistoryQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_ReadsThePageOfTheRetentionWindowWithTheFilters(
        SutProvider<ListInboxHistoryQuery> sutProvider, Guid userId, Guid collectionId, Guid requesterId,
        AccessRequestHistoryPage page)
    {
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetHistoryPageAsync(Arg.Any<AccessRequestHistoryFilter>()).Returns(page);
        var options = new AccessRequestHistoryQueryOptions
        {
            Page = 3,
            PageSize = 25,
            Statuses = [AccessRequestHistoryStatus.Revoked],
            CollectionIds = [collectionId],
            RequesterIds = [requesterId],
        };

        var result = await sutProvider.Sut.GetHistoryAsync(userId, _now, options);

        Assert.Same(page, result);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).GetHistoryPageAsync(
            Arg.Is<AccessRequestHistoryFilter>(filter =>
                filter.ManageableCollectionIds == manageable
                && filter.UserId == userId
                && filter.Since == _now.AddDays(-AccessHistoryWindow.RetentionDays)
                && filter.Now == _now
                && filter.Skip == 75
                && filter.Take == 25
                && filter.Statuses.SequenceEqual(options.Statuses)
                && filter.CollectionIds.SequenceEqual(options.CollectionIds)
                && filter.RequesterIds.SequenceEqual(options.RequesterIds)));
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_NoManageableCollections_StillReadsTheCallersOwnRequests(
        SutProvider<ListInboxHistoryQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        await sutProvider.Sut.GetHistoryAsync(userId, _now, new AccessRequestHistoryQueryOptions { PageSize = 10 });

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).GetHistoryPageAsync(
            Arg.Is<AccessRequestHistoryFilter>(filter =>
                filter.ManageableCollectionIds.Count == 0 && filter.UserId == userId && filter.Skip == 0));
    }
}
