using Bit.Pam.Entities;
using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class ListActiveLeasesQueryTests
{
    private static readonly DateTime _now = new(2026, 6, 5, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetActiveAsync_NoManageableCollections_ReturnsEmptyWithoutQuerying(Guid userId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetActiveAsync(userId);

        Assert.Empty(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .GetManyActiveByCollectionIdsAsync(default!, default);
    }

    [Theory, BitAutoData]
    public async Task GetActiveAsync_ManageableCollections_FiltersByThatSetAtNow(
        Guid userId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = Setup();
        var manageable = new HashSet<Guid> { collectionId };
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .GetManageableCollectionIdsAsync(userId).Returns(manageable);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByCollectionIdsAsync(manageable, _now).Returns([lease]);

        var result = await sutProvider.Sut.GetActiveAsync(userId);

        Assert.Single(result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .GetManyActiveByCollectionIdsAsync(manageable, _now);
    }

    private static SutProvider<ListActiveLeasesQuery> Setup()
    {
        var sutProvider = new SutProvider<ListActiveLeasesQuery>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
