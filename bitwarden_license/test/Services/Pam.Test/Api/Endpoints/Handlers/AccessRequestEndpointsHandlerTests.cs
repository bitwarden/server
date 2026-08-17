using System.Security.Claims;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.OrganizationFeatures.Commands.Interfaces;
using Bit.Services.Pam.OrganizationFeatures.Queries.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using ApiEnums = Bit.Services.Pam.Api.Models;

namespace Bit.Services.Pam.Test.Api.Endpoints;

[SutProviderCustomize]
public class AccessRequestEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();

    [Theory, BitAutoData]
    public async Task GetInbox_ReturnsMappedPendingRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListInboxRequestsQuery>().GetPendingAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetInbox(_user);

        Assert.Single(result.Data);
        Assert.Equal(row.Id, result.Data.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedHistoryRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IListInboxHistoryQuery>().GetHistoryAsync(userId).Returns([row]);

        var result = await sutProvider.Sut.GetHistory(_user);

        Assert.Single(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetMine_ReturnsMappedRows(
        Guid userId, AccessRequestDetails row, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([row]);

        var result = (await sutProvider.Sut.GetMine(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(row.Id, result[0].Id);
        Assert.Equal(ApiEnums.AccessRequestStatus.Pending, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_NoRows_ReturnsEmpty(
        Guid userId, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetMine(_user);

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetDetails_ReturnsMappedRow(
        Guid userId, Guid requestId, AccessRequestDetails details, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        details.Status = AccessRequestStatus.Approved;
        // A non-null produced lease would make the response model report Activated; null keeps the mapped status.
        details.ProducedLeaseId = null;
        sutProvider.GetDependency<IGetAccessRequestDetailsQuery>().GetDetailsAsync(userId, requestId).Returns(details);

        var result = await sutProvider.Sut.GetDetails(_user, requestId);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(ApiEnums.AccessRequestStatus.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Decide_ReturnsUpdatedRow(
        Guid userId, Guid requestId, AccessRequestDetails updated, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        updated.Status = AccessRequestStatus.Approved;
        updated.ProducedLeaseId = null;
        sutProvider.GetDependency<IDecideAccessRequestCommand>()
            .DecideAsync(userId, requestId, Arg.Any<AccessDecisionSubmission>())
            .Returns(updated);

        var result = await sutProvider.Sut.Decide(_user, requestId, new AccessDecisionRequestModel { Verdict = ApiEnums.AccessDecisionVerdict.Approve });

        Assert.Equal(updated.Id, result.Id);
        Assert.Equal(ApiEnums.AccessRequestStatus.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Activate_ReturnsMintedLease(
        Guid userId, Guid requestId, AccessLease lease, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IActivateAccessRequestCommand>()
            .ActivateAsync(userId, requestId)
            .Returns(lease);

        var result = await sutProvider.Sut.Activate(_user, requestId);

        Assert.Equal(lease.Id, result.Id);
        Assert.Equal(ApiEnums.AccessLeaseStatus.Active, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Revoke_InvokesCancelCommand(
        Guid userId, Guid requestId, SutProvider<AccessRequestEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);

        await sutProvider.Sut.Revoke(_user, requestId);

        await sutProvider.GetDependency<ICancelAccessRequestCommand>().Received(1).CancelAsync(userId, requestId);
    }

    private static void SetupUser(SutProvider<AccessRequestEndpointsHandler> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
