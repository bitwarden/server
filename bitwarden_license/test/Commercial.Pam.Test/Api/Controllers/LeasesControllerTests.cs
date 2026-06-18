using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Api.Pam.Models.Request;
using Bit.Api.Pam.Models.Response;
using Bit.Commercial.Pam.Models;
using Bit.Commercial.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Commercial.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Pam.Test.Api.Controllers;

[ControllerCustomize(typeof(LeasesController))]
[SutProviderCustomize]
public class LeasesControllerTests
{
    [Theory, BitAutoData]
    public async Task GetActive_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeasesController> sutProvider)
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
        Guid userId, SutProvider<LeasesController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetActive();

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeasesController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Revoked;
        sutProvider.GetDependency<IListLeaseHistoryQuery>().GetHistoryAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetHistory()).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatusNames.Revoked, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeasesController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IListMyActiveAccessLeasesQuery>().GetMineActiveAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetMine()).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatusNames.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task Revoke_ReturnsNoContent(
        Guid userId, Guid leaseId, SutProvider<LeasesController> sutProvider)
    {
        SetupUser(sutProvider, userId);

        var result = await sutProvider.Sut.Revoke(leaseId, new AccessLeaseRevokeRequestModel { Reason = "policy" });

        Assert.IsType<NoContentResult>(result);
        await sutProvider.GetDependency<IRevokeAccessLeaseCommand>().Received(1).RevokeAsync(userId, leaseId, "policy");
    }

    [Theory, BitAutoData]
    public async Task Extend_ForwardsRouteLeaseId_ReturnsApprovedExtensionDetails(
        Guid userId, Guid leaseId, AccessLeaseExtensionRequestModel model, AccessRequestDetails details,
        SutProvider<LeasesController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        details.Status = AccessRequestStatus.Approved;
        details.ProducedLeaseId = null; // an extension produces no lease of its own, so the status stays "approved"
        sutProvider.GetDependency<IRequestLeaseExtensionCommand>()
            .ExtendAsync(userId, Arg.Any<AccessLeaseExtensionSubmission>())
            .Returns(details);

        var result = await sutProvider.Sut.Extend(leaseId, model);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(AccessRequestStatusNames.Approved, result.Status);
        Assert.Equal(details.ExtensionOfLeaseId, result.ExtensionOfLeaseId);
        await sutProvider.GetDependency<IRequestLeaseExtensionCommand>().Received(1).ExtendAsync(
            userId,
            Arg.Is<AccessLeaseExtensionSubmission>(s =>
                s.LeaseId == leaseId && s.DurationSeconds == model.DurationSeconds && s.Reason == model.Reason));
    }

    private static void SetupUser(SutProvider<LeasesController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
