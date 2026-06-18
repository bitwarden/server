using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Commercial.Pam.Services;
using Bit.Pam.Entities;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class ListLeaseHistoryQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(Guid userId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .GetManyEndedByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_QueriesWithSharedRetentionWindow(
        Guid userId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = Setup();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        // Shares the approver inbox's history window.
        var expectedSince = _now.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyEndedByCollectionIdsAsync(manageable, expectedSince).Returns([lease]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId);

        Assert.Single(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyEndedByCollectionIdsAsync(manageable, expectedSince);
    }

    private static SutProvider<ListLeaseHistoryQuery> Setup()
    {
        var sutProvider = new SutProvider<ListLeaseHistoryQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
