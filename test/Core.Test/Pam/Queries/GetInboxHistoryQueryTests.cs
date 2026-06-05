using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class GetInboxHistoryQueryTests
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
        await sutProvider.GetDependency<ILeaseRequestRepository>().DidNotReceiveWithAnyArgs()
            .GetManyInboxHistoryByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetHistoryAsync_QueriesWithRetentionWindow(Guid userId, Guid collectionId, InboxLeaseRequestDetails row)
    {
        var sutProvider = Setup();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        var expectedSince = _now.AddDays(-GetInboxHistoryQuery.HistoryRetentionDays);
        sutProvider.GetDependency<ILeaseRequestRepository>()
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince).Returns([row]);

        var result = await sutProvider.Sut.GetHistoryAsync(userId);

        Assert.Single(result);
        await sutProvider.GetDependency<ILeaseRequestRepository>().Received(1)
            .GetManyInboxHistoryByCollectionIdsAsync(manageable, expectedSince);
    }

    private static SutProvider<GetInboxHistoryQuery> Setup()
    {
        var sutProvider = new SutProvider<GetInboxHistoryQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
