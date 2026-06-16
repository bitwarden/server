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

[ControllerCustomize(typeof(AccessRequestsController))]
[SutProviderCustomize]
public class AccessRequestsControllerTests
{
    [Theory, BitAutoData]
    public async Task GetInbox_ReturnsMappedPendingRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListInboxRequestsQuery>().GetPendingAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetInbox();

        Assert.Single(result.Data);
        Assert.Equal(row.Id, result.Data.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedHistoryRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IListInboxHistoryQuery>().GetHistoryAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetHistory();

        Assert.Single(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetMine_ReturnsMappedRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([row]);

        var result = (await sutProvider.Sut.GetMine()).Data.ToList();

        Assert.Single(result);
        Assert.Equal(row.Id, result[0].Id);
        Assert.Equal(AccessRequestStatusNames.Pending, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_NoRows_ReturnsEmpty(
        Guid userId, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetMine();

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task Decide_ReturnsUpdatedRow(
        Guid userId, Guid requestId, AccessRequestDetails updated, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);
        updated.Status = AccessRequestStatus.Approved;
        updated.ProducedLeaseId = null;
        sutProvider.GetDependency<IDecideAccessRequestCommand>()
            .DecideAsync(userId, requestId, Arg.Any<AccessDecisionSubmission>())
            .Returns(updated);

        var result = await sutProvider.Sut.Decide(requestId, new AccessDecisionRequestModel { Verdict = AccessDecisionVerdict.Approve });

        Assert.Equal(updated.Id, result.Id);
        Assert.Equal(AccessRequestStatusNames.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Activate_ReturnsMintedLease(
        Guid userId, Guid requestId, AccessLease lease, SutProvider<AccessRequestsController> sutProvider)
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
    public async Task Revoke_RevokesCallersRequest_ReturnsNoContent(
        Guid userId, Guid requestId, SutProvider<AccessRequestsController> sutProvider)
    {
        SetupUser(sutProvider, userId);

        var result = await sutProvider.Sut.Revoke(requestId);

        Assert.IsType<NoContentResult>(result);
        await sutProvider.GetDependency<ICancelAccessRequestCommand>().Received(1).CancelAsync(userId, requestId);
    }

    private static void SetupUser(SutProvider<AccessRequestsController> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
