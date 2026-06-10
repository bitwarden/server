using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Pam.Controllers;

[ControllerCustomize(typeof(MemberLeasingController))]
[SutProviderCustomize]
public class MemberLeasingControllerTests
{
    [Theory, BitAutoData]
    public async Task GetMyRequests_ReturnsMappedRows(
        Guid userId, AccessRequestDetails row, SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([row]);

        var result = (await sutProvider.Sut.GetMyRequests()).ToList();

        Assert.Single(result);
        Assert.Equal(row.Id, result[0].Id);
        Assert.Equal(AccessRequestStatusNames.Pending, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMyActiveLeases_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IListMyActiveAccessLeasesQuery>().GetMineActiveAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetMyActiveLeases()).ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatusNames.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMyRequests_NoRows_ReturnsEmpty(
        Guid userId, SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetMyRequests();

        Assert.Empty(result);
    }

    private static void SetupUser(SutProvider<MemberLeasingController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
