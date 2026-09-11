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
public class ListInboxRequestsQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetPendingAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(
        SutProvider<ListInboxRequestsQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetPendingAsync(userId, _now);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .GetManyInboxPendingByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetPendingAsync_ManageableCollections_FiltersByThatSet(
        SutProvider<ListInboxRequestsQuery> sutProvider, Guid userId, Guid collectionId, AccessRequestDetails row)
    {
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyInboxPendingByCollectionIdsAsync(manageable, _now).Returns([row]);

        var result = await sutProvider.Sut.GetPendingAsync(userId, _now);

        Assert.Single(result);
        // The caller's clock is forwarded unchanged: the same instant filters the read and stamps the statuses.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .GetManyInboxPendingByCollectionIdsAsync(manageable, _now);
    }
}
