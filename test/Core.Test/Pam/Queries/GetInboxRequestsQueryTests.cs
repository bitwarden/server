using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class GetInboxRequestsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetPendingAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(
        SutProvider<GetInboxRequestsQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetPendingAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<ILeaseRequestRepository>().DidNotReceiveWithAnyArgs()
            .GetManyInboxPendingByCollectionIdsAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task GetPendingAsync_ManageableCollections_FiltersByThatSet(
        SutProvider<GetInboxRequestsQuery> sutProvider, Guid userId, Guid collectionId, InboxLeaseRequestDetails row)
    {
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        sutProvider.GetDependency<ILeaseRequestRepository>()
            .GetManyInboxPendingByCollectionIdsAsync(manageable).Returns([row]);

        var result = await sutProvider.Sut.GetPendingAsync(userId);

        Assert.Single(result);
        await sutProvider.GetDependency<ILeaseRequestRepository>().Received(1)
            .GetManyInboxPendingByCollectionIdsAsync(manageable);
    }
}
