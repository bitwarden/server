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

[ControllerCustomize(typeof(LeaseGovernanceController))]
[SutProviderCustomize]
public class LeaseGovernanceControllerTests
{
    [Theory, BitAutoData]
    public async Task GetActive_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeaseGovernanceController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetActive()).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatusNames.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetActive_NoLeases_ReturnsEmpty(
        Guid userId, SutProvider<LeaseGovernanceController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetActive();

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeaseGovernanceController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Revoked;
        sutProvider.GetDependency<IListLeaseHistoryQuery>().GetHistoryAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetHistory()).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatusNames.Revoked, result[0].Status);
    }

    private static void SetupUser(SutProvider<LeaseGovernanceController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
