using System.Security.Claims;
using Bit.Api.Pam.Controllers;
using Bit.Api.Pam.Models.Request;
using Bit.Core.Exceptions;
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

[ControllerCustomize(typeof(ApproverInboxController))]
[SutProviderCustomize]
public class ApproverInboxControllerTests
{
    [Theory, BitAutoData]
    public async Task GetRequests_ReturnsMappedPendingRows(
        Guid userId, AccessRequestDetails row, SutProvider<ApproverInboxController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListInboxRequestsQuery>().GetPendingAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetRequests();

        Assert.Single(result.Data);
        Assert.Equal(row.Id, result.Data.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedHistoryRows(
        Guid userId, AccessRequestDetails row, SutProvider<ApproverInboxController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IListInboxHistoryQuery>().GetHistoryAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetHistory();

        Assert.Single(result.Data);
    }

    [Theory, BitAutoData]
    public async Task Decide_ReturnsUpdatedRow(
        Guid userId, Guid requestId, AccessRequestDetails updated, SutProvider<ApproverInboxController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        updated.Status = AccessRequestStatus.Approved;
        updated.ProducedLeaseId = null;
        sutProvider.GetDependency<IDecideAccessRequestCommand>()
            .DecideAsync(userId, requestId, Arg.Any<AccessDecisionSubmission>())
            .Returns(updated);

        var result = await sutProvider.Sut.Decide(requestId, new AccessDecisionRequestModel { Verdict = "approve" });

        Assert.Equal(updated.Id, result.Id);
        Assert.Equal(AccessRequestStatusNames.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Decide_InvalidDecision_ThrowsBadRequest(
        Guid userId, Guid requestId, SutProvider<ApproverInboxController> sutProvider)
    {
        SetupUser(sutProvider, userId);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.Decide(requestId, new AccessDecisionRequestModel { Verdict = "maybe" }));
    }

    [Theory, BitAutoData]
    public async Task Revoke_ReturnsNoContent(
        Guid userId, Guid leaseId, SutProvider<ApproverInboxController> sutProvider)
    {
        SetupUser(sutProvider, userId);

        var result = await sutProvider.Sut.Revoke(leaseId, new AccessLeaseRevokeRequestModel { Reason = "policy" });

        Assert.IsType<NoContentResult>(result);
        await sutProvider.GetDependency<IRevokeAccessLeaseCommand>().Received(1).RevokeAsync(userId, leaseId, "policy");
    }

    private static void SetupUser(SutProvider<ApproverInboxController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
