using Bit.Pam.Entities;
using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class ListMyActiveAccessLeasesQueryTests
{
    [Theory, BitAutoData]
    public async Task GetMineActiveAsync_ReturnsActiveLeases(
        SutProvider<ListMyActiveAccessLeasesQuery> sutProvider, Guid userId, AccessLease lease)
    {
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns([lease]);

        var result = await sutProvider.Sut.GetMineActiveAsync(userId);

        Assert.Single(result);
        Assert.Equal(lease.Id, result.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetMineActiveAsync_NoLeases_ReturnsEmpty(
        SutProvider<ListMyActiveAccessLeasesQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns([]);

        var result = await sutProvider.Sut.GetMineActiveAsync(userId);

        Assert.Empty(result);
    }
}
