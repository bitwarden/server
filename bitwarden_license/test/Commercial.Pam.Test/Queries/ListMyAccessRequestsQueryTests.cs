using Bit.Pam.Models;
using Bit.Commercial.Pam.OrganizationFeatures.Queries;
using Bit.Pam.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Queries;

[SutProviderCustomize]
public class ListMyAccessRequestsQueryTests
{
    [Theory, BitAutoData]
    public async Task GetMineAsync_ReturnsRequesterRows(
        SutProvider<ListMyAccessRequestsQuery> sutProvider, Guid userId, AccessRequestDetails row)
    {
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId)
            .Returns([row]);

        var result = await sutProvider.Sut.GetMineAsync(userId);

        Assert.Single(result);
        Assert.Equal(row.Id, result.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetMineAsync_NoRequests_ReturnsEmpty(
        SutProvider<ListMyAccessRequestsQuery> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetManyByRequesterIdAsync(userId)
            .Returns([]);

        var result = await sutProvider.Sut.GetMineAsync(userId);

        Assert.Empty(result);
    }
}
