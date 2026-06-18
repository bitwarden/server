using Bit.Pam.Models;
using Bit.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

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

        var result = await sutProvider.Sut.GetHistoryAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .GetManyInboxHistoryByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_QueriesWithRetentionWindow(Guid userId, Guid collectionId, AccessRequestDetails row)
    {
        var sutProvider = Setup();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        var expectedSince = _now.AddDays(-ListInboxHistoryQuery.HistoryRetentionDays);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince).Returns([row]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId);

        Assert.Single(result);
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince);
    }

    private static SutProvider<ListInboxHistoryQuery> Setup()
    {
        var sutProvider = new SutProvider<ListInboxHistoryQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
