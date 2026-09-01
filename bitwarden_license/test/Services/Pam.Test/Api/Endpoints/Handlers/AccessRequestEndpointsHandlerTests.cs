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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Endpoints;

[SutProviderCustomize]
public class AccessRequestEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();

    // A pinned clock far from the wall clock on purpose: a derivation that accidentally reads the real clock instead
    // of the handler's TimeProvider lands on the wrong side of every window built from _now and fails loudly.
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetInbox_ReturnsMappedPendingRows(Guid userId, AccessRequestDetails row)
    {
        var sutProvider = Setup(userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListInboxRequestsQuery>().GetPendingAsync(userId, _now).Returns([row]);

        var result = await sutProvider.Sut.GetInbox(_user);

        Assert.Single(result.Data);
        Assert.Equal(row.Id, result.Data.First().Id);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedHistoryRows(Guid userId, AccessRequestDetails row)
    {
        var sutProvider = Setup(userId);
        row.Status = AccessRequestStatus.Approved;
        sutProvider.GetDependency<IListInboxHistoryQuery>().GetHistoryAsync(userId, _now).Returns([row]);

        var result = await sutProvider.Sut.GetHistory(_user);

        Assert.Single(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetMine_ReturnsMappedRows(Guid userId, AccessRequestDetails row)
    {
        var sutProvider = Setup(userId);
        row.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId, _now).Returns([row]);

        var result = (await sutProvider.Sut.GetMine(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(row.Id, result[0].Id);
        Assert.Equal(AccessRequestStatus.Pending, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_NoRows_ReturnsEmpty(Guid userId)
    {
        var sutProvider = Setup(userId);
        sutProvider.GetDependency<IListMyAccessRequestsQuery>().GetMineAsync(userId, _now).Returns([]);

        var result = await sutProvider.Sut.GetMine(_user);

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetDetails_ReturnsMappedRow(Guid userId, Guid requestId, AccessRequestDetails details)
    {
        var sutProvider = Setup(userId);
        details.Status = AccessRequestStatus.Approved;
        // No produced lease: the request keeps its own status, which the response model passes through verbatim.
        details.ProducedLeaseId = null;
        sutProvider.GetDependency<IGetAccessRequestDetailsQuery>()
            .GetDetailsAsync(userId, requestId, _now).Returns(details);

        var result = await sutProvider.Sut.GetDetails(_user, requestId);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(AccessRequestStatus.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Decide_ReturnsUpdatedRow(Guid userId, Guid requestId, AccessRequestDetails updated)
    {
        var sutProvider = Setup(userId);
        updated.Status = AccessRequestStatus.Approved;
        updated.ProducedLeaseId = null;
        sutProvider.GetDependency<IDecideAccessRequestCommand>()
            .DecideAsync(userId, requestId, Arg.Any<AccessDecisionSubmission>())
            .Returns(updated);

        var result = await sutProvider.Sut.Decide(_user, requestId, new AccessDecisionRequestModel { Verdict = AccessDecisionVerdict.Approve });

        Assert.Equal(updated.Id, result.Id);
        Assert.Equal(AccessRequestStatus.Approved, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Activate_DerivesResponseAgainstTheClockItGaveTheCommand(
        Guid userId, Guid requestId, AccessLease lease)
    {
        var sutProvider = Setup(userId);
        // Live only relative to the pinned clock: no early end, window open at _now (and long lapsed in wall-clock
        // terms). The Active assertion below therefore proves the response derived against the same instant the
        // handler handed the command -- a second, later clock read would report the granted lease as expired.
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IActivateAccessRequestCommand>()
            .ActivateAsync(userId, requestId, _now)
            .Returns(lease);

        var result = await sutProvider.Sut.Activate(_user, requestId);

        Assert.Equal(lease.Id, result.Id);
        Assert.Equal(AccessLeaseStatus.Active, result.Status);
    }

    [Theory, BitAutoData]
    public async Task Revoke_InvokesCancelCommand(Guid userId, Guid requestId)
    {
        var sutProvider = Setup(userId);

        await sutProvider.Sut.Revoke(_user, requestId);

        await sutProvider.GetDependency<ICancelAccessRequestCommand>().Received(1).CancelAsync(userId, requestId);
    }

    private static SutProvider<AccessRequestEndpointsHandler> Setup(Guid userId)
    {
        var sutProvider = new SutProvider<AccessRequestEndpointsHandler>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
        return sutProvider;
    }
}
