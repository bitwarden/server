using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Services.Pam.OrganizationFeatures.Queries;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Queries;

[SutProviderCustomize]
public class ListLeaseHistoryQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(Guid userId)
    {
        var sutProvider = new SutProvider<ListLeaseHistoryQuery>().Create();
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId, _now);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .GetManyEndedByCollectionIdsAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_QueriesWithSharedRetentionWindow(
        Guid userId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = new SutProvider<ListLeaseHistoryQuery>().Create();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        // Shares the one history window with the approver inbox.
        var expectedSince = _now.AddDays(-AccessHistoryWindow.RetentionDays);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyEndedByCollectionIdsAsync(manageable, expectedSince, _now).Returns([lease]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId, _now);

        Assert.Single(result);
        // The caller's clock is passed alongside `since`: it is what decides a lapsed lease has ended at all, since
        // nothing writes Expired (PM-42355), and the caller derives response statuses against the same instant.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyEndedByCollectionIdsAsync(manageable, expectedSince, _now);
    }
}
