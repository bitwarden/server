using System.Security.Claims;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
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
public class LeaseEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();

    // A pinned clock far from the wall clock on purpose: a derivation that accidentally reads the real clock instead
    // of the handler's TimeProvider lands on the wrong side of every window built from _now and fails loudly.
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task GetActive_DerivesStatusAgainstTheClockThatFilteredTheRead(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup(userId);
        // Live only relative to the pinned clock: no early end + a window open at _now reads as Active. The exact-
        // argument mock proves the filter clock and the derivation clock are one value.
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId, _now).Returns([lease]);

        var result = (await sutProvider.Sut.GetActive(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatus.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetActive_NoLeases_ReturnsEmpty(Guid userId)
    {
        var sutProvider = Setup(userId);
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId, _now).Returns([]);

        var result = await sutProvider.Sut.GetActive(_user);

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedLeases(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup(userId);
        lease.Action = AccessLeaseAction.Revoked;
        sutProvider.GetDependency<IListLeaseHistoryQuery>().GetHistoryAsync(userId, _now).Returns([lease]);

        var result = (await sutProvider.Sut.GetHistory(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatus.Revoked, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_DerivesStatusAgainstTheClockThatFilteredTheRead(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup(userId);
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetManyActiveByRequesterIdAsync(userId, _now)
            .Returns([lease]);

        var result = (await sutProvider.Sut.GetMine(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(AccessLeaseStatus.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task Revoke_InvokesRevokeCommand(Guid userId, Guid leaseId)
    {
        var sutProvider = Setup(userId);

        await sutProvider.Sut.Revoke(_user, leaseId, new AccessLeaseRevokeRequestModel { Reason = "policy" });

        await sutProvider.GetDependency<IRevokeAccessLeaseCommand>().Received(1).RevokeAsync(userId, leaseId, "policy");
    }

    [Theory, BitAutoData]
    public async Task Extend_ForwardsRouteLeaseId_ReturnsApprovedExtensionDetails(
        Guid userId, Guid leaseId, AccessLeaseExtensionRequestModel model, AccessRequestDetails details)
    {
        var sutProvider = Setup(userId);
        details.Status = AccessRequestStatus.Approved;
        details.ProducedLeaseId = null; // an extension produces no lease of its own, so the status stays Approved
        sutProvider.GetDependency<IRequestLeaseExtensionCommand>()
            .ExtendAsync(userId, Arg.Any<AccessLeaseExtensionSubmission>())
            .Returns(details);

        var result = await sutProvider.Sut.Extend(_user, leaseId, model);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Equal(details.ExtensionOfLeaseId, result.ExtensionOfLeaseId);
        await sutProvider.GetDependency<IRequestLeaseExtensionCommand>().Received(1).ExtendAsync(
            userId,
            Arg.Is<AccessLeaseExtensionSubmission>(s =>
                s.LeaseId == leaseId && s.DurationSeconds == model.DurationSeconds && s.Reason == model.Reason));
    }

    private static SutProvider<LeaseEndpointsHandler> Setup(Guid userId)
    {
        var sutProvider = new SutProvider<LeaseEndpointsHandler>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
        return sutProvider;
    }
}
