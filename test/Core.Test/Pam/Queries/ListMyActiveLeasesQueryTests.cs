using Bit.Core.Pam.Entities;
using Bit.Core.Pam.OrganizationFeatures.Queries;
using Bit.Core.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Queries;

[SutProviderCustomize]
public class ListMyActiveLeasesQueryTests
{
    [Theory, BitAutoData]
    public async Task GetMineActiveAsync_ReturnsActiveLeases(
        SutProvider<ListMyActiveLeasesQuery> sutProvider, Guid userId, Lease lease)
    {
        sutProvider.GetDependency<ILeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns([lease]);

        var result = await sutProvider.Sut.GetMineActiveAsync(userId);

        Assert.Single(result);
        Assert.Equal(lease.Id, result.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetMineActiveAsync_NoLeases_ReturnsEmpty(
        SutProvider<ListMyActiveLeasesQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<ILeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, Arg.Any<DateTime>())
            .Returns([]);

        var result = await sutProvider.Sut.GetMineActiveAsync(userId);

        Assert.Empty(result);
    }
}
