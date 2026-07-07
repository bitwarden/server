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
public class LeaseEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();

    [Theory, BitAutoData]
    public async Task GetActive_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetActive(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(ApiEnums.AccessLeaseStatus.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetActive_NoLeases_ReturnsEmpty(
        Guid userId, SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        sutProvider.GetDependency<IListActiveLeasesQuery>().GetActiveAsync(userId).Returns([]);

        var result = await sutProvider.Sut.GetActive(_user);

        Assert.Empty(result.Data);
    }

    [Theory, BitAutoData]
    public async Task GetHistory_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Revoked;
        sutProvider.GetDependency<IListLeaseHistoryQuery>().GetHistoryAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetHistory(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(ApiEnums.AccessLeaseStatus.Revoked, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task GetMine_ReturnsMappedLeases(
        Guid userId, AccessLease lease, SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IListMyActiveAccessLeasesQuery>().GetMineActiveAsync(userId).Returns([lease]);

        var result = (await sutProvider.Sut.GetMine(_user)).Data.ToList();

        Assert.Single(result);
        Assert.Equal(lease.Id, result[0].Id);
        Assert.Equal(ApiEnums.AccessLeaseStatus.Active, result[0].Status);
    }

    [Theory, BitAutoData]
    public async Task Revoke_InvokesRevokeCommand(
        Guid userId, Guid leaseId, SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);

        await sutProvider.Sut.Revoke(_user, leaseId, new AccessLeaseRevokeRequestModel { Reason = "policy" });

        await sutProvider.GetDependency<IRevokeAccessLeaseCommand>().Received(1).RevokeAsync(userId, leaseId, "policy");
    }

    [Theory, BitAutoData]
    public async Task Extend_ForwardsRouteLeaseId_ReturnsApprovedExtensionDetails(
        Guid userId, Guid leaseId, AccessLeaseExtensionRequestModel model, AccessRequestDetails details,
        SutProvider<LeaseEndpointsHandler> sutProvider)
    {
        SetupUser(sutProvider, userId);
        details.Status = AccessRequestStatus.Approved;
        details.ProducedLeaseId = null; // an extension produces no lease of its own, so the status stays Approved
        sutProvider.GetDependency<IRequestLeaseExtensionCommand>()
            .ExtendAsync(userId, Arg.Any<AccessLeaseExtensionSubmission>())
            .Returns(details);

        var result = await sutProvider.Sut.Extend(_user, leaseId, model);

        Assert.Equal(details.Id, result.Id);
        Assert.Equal(ApiEnums.AccessRequestStatus.Approved, result.Status);
        Assert.Equal(details.ExtensionOfLeaseId, result.ExtensionOfLeaseId);
        await sutProvider.GetDependency<IRequestLeaseExtensionCommand>().Received(1).ExtendAsync(
            userId,
            Arg.Is<AccessLeaseExtensionSubmission>(s =>
                s.LeaseId == leaseId && s.DurationSeconds == model.DurationSeconds && s.Reason == model.Reason));
    }

    private static void SetupUser(SutProvider<LeaseEndpointsHandler> sutProvider, Guid userId)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
    }
}
