using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
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
    public async Task CancelAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CancelAsync(userId, requestId));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_NeitherRequesterNorManager_ThrowsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.None;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        // userId is neither the requester nor a manager (CanManageCollectionAsync defaults to false).

        // A request the caller can't act on is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.CancelAsync(userId, request.Id));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessRequestAction.Denied)]
    [BitAutoData(AccessRequestAction.Cancelled)]
    public async Task CancelAsync_TerminalAction_ThrowsConflict(AccessRequestAction action, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = action;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory]
    [BitAutoData(AccessRequestAction.None)]
    [BitAutoData(AccessRequestAction.Approved)]
    public async Task CancelAsync_WindowLapsed_ThrowsConflict(AccessRequestAction action, AccessRequest request)
    {
        // A lapsed row is derived Expired everywhere it is read; a cancellation must not restamp it.
        var sutProvider = Setup();
        request.Action = action;
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));
        Assert.Contains("already ended", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessRequestAction.None)]
    [BitAutoData(AccessRequestAction.Approved)]
    public async Task CancelAsync_RequesterNoLease_CancelsAndNotifies(AccessRequestAction action, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = action;
        SetOpenWindow(request);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        // No lease produced (GetByAccessRequestIdAsync defaults to null).

        await sutProvider.Sut.CancelAsync(request.RequesterId, request.Id);

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CancelAsync(request.Id, _now);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
    }

    [Theory]
    [BitAutoData(AccessRequestAction.None)]
    [BitAutoData(AccessRequestAction.Approved)]
    public async Task CancelAsync_ManagerNoLease_DeniesWithDecisionAndNotifies(
        AccessRequestAction action, Guid managerId, AccessRequest request)
    {
        var sutProvider = Setup();
        request.Action = action;
        SetOpenWindow(request);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IApproverCollectionAccessQuery>()
            .CanManageCollectionAsync(managerId, request.CollectionId).Returns(true);

        await sutProvider.Sut.CancelAsync(managerId, request.Id);

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
    public async Task CancelAsync_ApprovedWithActiveLease_ThrowsConflict(AccessRequest request, AccessLease lease)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.Approved;
        SetOpenWindow(request);
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(lease);

        var conflict = await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));
        // A live lease is ended through revoke, so the caller is pointed there.
        Assert.Contains("revoke the lease instead", conflict.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelWithDecisionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessLeaseAction.Revoked)]
    [BitAutoData(AccessLeaseAction.Cancelled)]
    public async Task CancelAsync_ApprovedWithEndedLease_ThrowsConflict(
        AccessLeaseAction leaseAction, AccessRequest request, AccessLease lease)
    {
        var sutProvider = Setup();
        request.Action = AccessRequestAction.Approved;
        SetOpenWindow(request);
        lease.Action = leaseAction;
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(lease);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    [Theory, BitAutoData]
    public async Task CancelAsync_ApprovedWithLapsedLease_ReportsAlreadyResolvedRatherThanPointingAtRevoke(
        AccessRequest request, AccessLease lease)
    {
        // A lease whose window has closed carries no early end, so judging the recorded action alone would send the
        // caller to a Revoke that revoke itself refuses. The lease has already ended: the request is terminal
        // history (PM-42355). The request's own window is pinned open so this exercises the lease branch.
        var sutProvider = Setup();
        request.Action = AccessRequestAction.Approved;
        SetOpenWindow(request);
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddMinutes(-1);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(lease);

        var conflict = await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.CancelAsync(request.RequesterId, request.Id));

        Assert.Contains("already been resolved", conflict.Message);
        Assert.DoesNotContain("revoke the lease instead", conflict.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CancelAsync(default, default);
    }

    // BitAutoData generates arbitrary dates; pin a window containing _now so the lapsed-window guard doesn't trip
    // in tests that aren't about it.
    private static void SetOpenWindow(AccessRequest request)
    {
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
    }

    private static SutProvider<CancelAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<CancelAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }
}
