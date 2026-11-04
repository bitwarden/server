using System.Security.Claims;
using Bit.Core.Services;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Services.Pam.Api.Endpoints.Handlers;
using Bit.Services.Pam.Api.Models.Request;
using Bit.Services.Pam.Enums;
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
public class CipherLeaseEndpointsHandlerTests
{
    private static readonly ClaimsPrincipal _user = new();
    private static readonly DateTime _now = new(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task State_ReturnsSnapshotFromQuery(
        Guid id, Guid userId, Bit.Pam.Entities.AccessLease activeLease, SutProvider<CipherLeaseEndpointsHandler> sutProvider)
    {
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
        sutProvider.GetDependency<IGetCipherAccessStateQuery>()
            .GetStateAsync(userId, id)
            .Returns(new Bit.Services.Pam.Models.CipherAccessState(id, DateTime.UtcNow, activeLease, null, null));

        var result = await sutProvider.Sut.State(_user, id);

        Assert.Equal(id, result.CipherId);
        Assert.NotNull(result.ActiveLease);
        Assert.Equal(activeLease.Id, result.ActiveLease!.Id);
        Assert.Null(result.PendingRequest);
        Assert.Null(result.ApprovedRequest);
    }

    [Theory, BitAutoData]
    public async Task Post_SubmitsForTheCallerAndProjectsTheResult(Guid id, Guid userId, AccessRequest request)
    {
        var sutProvider = new SutProvider<CipherLeaseEndpointsHandler>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        sutProvider.GetDependency<IUserService>()
            .GetProperUserId(Arg.Any<ClaimsPrincipal>())
            .Returns(userId);
        request.Action = AccessRequestAction.None;
        request.ExtensionOfLeaseId = null;
        request.NotBefore = _now;
        request.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<ISubmitAccessRequestCommand>()
            .SubmitAsync(userId, id, Arg.Any<AccessRequestSubmission>())
            .Returns(AccessRequestResult.Human(request));
        var model = new AccessRequestCreateRequestModel
        {
            Start = _now,
            End = _now.AddHours(1),
            Reason = "Rotate the database credentials",
        };

        var result = await sutProvider.Sut.Post(_user, id, model);

        await sutProvider.GetDependency<ISubmitAccessRequestCommand>().Received(1).SubmitAsync(
            userId,
            id,
            Arg.Is<AccessRequestSubmission>(s =>
                s.Start == model.Start && s.End == model.End && s.Reason == model.Reason && s.DurationSeconds == null));
        Assert.Equal(AccessApprovalMode.Human, result.ApprovalMode);
        Assert.Equal(request.Id, result.Request.Id);
        // Derived against the handler's clock: an unanswered request inside its window is pending.
        Assert.Equal(AccessRequestStatus.Pending, result.Request.Status);
    }
}
