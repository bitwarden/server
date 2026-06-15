using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Api.Pam.Models.Request;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Core.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Mvc;
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

        var result = (await sutProvider.Sut.GetMyRequests()).Data.ToList();

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

        var result = (await sutProvider.Sut.GetMyActiveLeases()).Data.ToList();

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

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task Activate_ReturnsMintedLease(
        Guid userId, Guid requestId, AccessLease lease, SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IActivateAccessRequestCommand>()
            .ActivateAsync(userId, requestId)
            .Returns(lease);

        var result = await sutProvider.Sut.Activate(requestId);

        Assert.Equal(lease.Id, result.Id);
        Assert.Equal(AccessLeaseStatusNames.Active, result.Status);
    }

    [Theory, BitAutoData]
    public async Task RevokeRequest_RevokesCallersRequest_ReturnsNoContent(
        Guid userId, Guid requestId, SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);

        var result = await sutProvider.Sut.RevokeRequest(requestId);

        Assert.IsType<NoContentResult>(result);
        await sutProvider.GetDependency<ICancelAccessRequestCommand>().Received(1).CancelAsync(userId, requestId);
    }

    [Theory, BitAutoData]
    public async Task RequestExtension_ForwardsSubmission_ReturnsApprovedExtensionDetails(
        Guid userId, AccessLeaseExtensionRequestModel model, AccessRequestDetails details,
        SutProvider<MemberLeasingController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        details.Status = AccessRequestStatus.Approved;
        details.ProducedLeaseId = null; // an extension produces no lease of its own, so the status stays "approved"
        sutProvider.GetDependency<IRequestLeaseExtensionCommand>()
            .ExtendAsync(userId, Arg.Any<AccessLeaseExtensionSubmission>())
            .Returns(details);

        var result = await sutProvider.Sut.RequestExtension(model);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(AccessRequestStatusNames.Approved, result.Status);
        Assert.Equal(details.ExtensionOfLeaseId, result.ExtensionOfLeaseId);
        await sutProvider.GetDependency<IRequestLeaseExtensionCommand>().Received(1).ExtendAsync(
            userId,
            Arg.Is<AccessLeaseExtensionSubmission>(s =>
                s.LeaseId == model.LeaseId && s.DurationSeconds == model.DurationSeconds && s.Reason == model.Reason));
    }

    private static void SetupUser(SutProvider<MemberLeasingController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
