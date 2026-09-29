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
public class ListActiveLeasesQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetActiveAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(Guid userId)
    {
        var sutProvider = new SutProvider<ListActiveLeasesQuery>().Create();
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetActiveAsync(userId, _now);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .GetManyActiveByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetActiveAsync_ManageableCollections_FiltersByThatSetAtNow(
        Guid userId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = new SutProvider<ListActiveLeasesQuery>().Create();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByCollectionIdsAsync(manageable, _now).Returns([lease]);

        var result = await sutProvider.Sut.GetActiveAsync(userId, _now);

        Assert.Single(result);
        // The caller's clock is forwarded unchanged: the same instant filters the read and derives the response.
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyActiveByCollectionIdsAsync(manageable, _now);
    }
}
