using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Errors;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class CancelAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task CancelAsync_RequestMissing_ReturnsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        var result = await sutProvider.Sut.CancelAsync(userId, requestId);

        Assert.IsType<AccessRequestNotFound>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_NeitherRequesterNorManager_ReturnsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Pending;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        // userId is neither the requester nor a manager (CanManageCollectionAsync defaults to false).

        // A request the caller can't act on is indistinguishable from a missing one, so ids can't be probed.
        var result = await sutProvider.Sut.CancelAsync(userId, request.Id);

        Assert.IsType<AccessRequestNotFound>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessRequestStatus.Denied)]
    [BitAutoData(AccessRequestStatus.Cancelled)]
    [BitAutoData(AccessRequestStatus.Expired)]
    public async Task CancelAsync_TerminalStatus_ReturnsConflict(AccessRequestStatus status, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = status;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        var result = await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessRequestAlreadyResolved>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory]
    [BitAutoData(AccessRequestStatus.Pending)]
    [BitAutoData(AccessRequestStatus.Approved)]
    public async Task CancelAsync_RequesterNoLease_CancelsAndNotifies(AccessRequestStatus status, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = status;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        // No lease produced (GetByAccessRequestIdAsync defaults to null).

        (await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id)).AssertSuccess();

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CancelAsync(request.Id, _now);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
    }

    [Theory]
    [BitAutoData(AccessRequestStatus.Pending)]
    [BitAutoData(AccessRequestStatus.Approved)]
    public async Task CancelAsync_ManagerNoLease_DeniesWithDecisionAndNotifies(
        AccessRequestStatus status, Guid managerId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Status = status;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(managerId, request.CollectionId).Returns(true);

        (await sutProvider.Sut.CancelAsync(managerId, request.Id)).AssertSuccess();

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CancelWithDecisionAsync(
            request,
            Arg.Is<AccessDecision>(d =>
                d.AccessRequestId == request.Id
                && d.ApproverId == managerId
                && d.Verdict == AccessDecisionVerdict.Deny
                && d.DeciderKind == AccessDeciderKind.Human),
            _now);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_ApprovedWithActiveLease_ReturnsConflict(AccessRequest request, AccessLease lease)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Approved;
        lease.Status = AccessLeaseStatus.Active;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(lease);

        var result = await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessRequestHasActiveLease>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessLeaseStatus.Revoked)]
    [BitAutoData(AccessLeaseStatus.Expired)]
    public async Task CancelAsync_ApprovedWithEndedLease_ReturnsConflict(
        AccessLeaseStatus leaseStatus, AccessRequest request, AccessLease lease)
    {
        var sutProvider = Setup();
        request.Status = AccessRequestStatus.Approved;
        lease.Status = leaseStatus;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(lease);

        var result = await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessRequestAlreadyResolved>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    private static SutProvider<CancelAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<CancelAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
