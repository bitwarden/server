using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
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
    public async Task GetHistoryAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(Guid userId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId, _now);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .GetManyInboxHistoryByCollectionIdsAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_QueriesWithRetentionWindow(Guid userId, Guid collectionId, AccessRequestDetails row)
    {
        var sutProvider = Setup();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        var expectedSince = _now.AddDays(-AccessHistoryWindow.RetentionDays);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince, _now).Returns([row]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId, _now);

        Assert.Single(result);
        // `now` is passed alongside `since`: it is the clock each row's produced-lease status is projected against
        // (PM-42355), distinct from the window bound.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince, _now);
    }

    private static SutProvider<ListInboxHistoryQuery> Setup()
    {
        var sutProvider = new SutProvider<ListInboxHistoryQuery>().Create();
        return sutProvider;
    }
}
