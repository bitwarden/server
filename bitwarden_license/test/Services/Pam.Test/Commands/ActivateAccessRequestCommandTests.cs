using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
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
public class ActivateAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task ActivateAsync_RequestMissing_ReturnsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        var result = await sutProvider.Sut.ActivateAsync(userId, requestId);

        Assert.IsType<AccessRequestNotFound>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_NotOwner_ReturnsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);

        // Someone else's request is indistinguishable from a missing one, so ids can't be probed.
        var result = await sutProvider.Sut.ActivateAsync(userId, request.Id);

        Assert.IsType<AccessRequestNotFound>(result.AssertError());
    }

    // Pending is told apart from the terminal statuses: a pending request becomes activatable once an approver acts,
    // while a denied, cancelled or expired one never will, and the two codes let a client say so.
    [Theory]
    [BitAutoData(AccessRequestStatus.Pending, typeof(AccessRequestNotApproved))]
    [BitAutoData(AccessRequestStatus.Denied, typeof(AccessRequestNotActivatable))]
    [BitAutoData(AccessRequestStatus.Cancelled, typeof(AccessRequestNotActivatable))]
    [BitAutoData(AccessRequestStatus.Expired, typeof(AccessRequestNotActivatable))]
    public async Task ActivateAsync_NotApproved_ReturnsConflict(
        AccessRequestStatus status, Type expectedError, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.Status = status;

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType(expectedError, result.AssertError());
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_LiveLease_ReturnsExistingWithoutMinting(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Status = AccessLeaseStatus.Active;
        existing.NotAfter = _now.AddMinutes(30);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        var result = (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        Assert.Same(existing, result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory]
    [BitAutoData(AccessLeaseStatus.Revoked)]
    [BitAutoData(AccessLeaseStatus.Expired)]
    public async Task ActivateAsync_AlreadyActivated_DeadLease_ReturnsConflict(
        AccessLeaseStatus leaseStatus, AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Status = leaseStatus;
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        // A request authorizes access at most once; a revoked or lapsed lease is final.
        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessLeaseAlreadyUsed>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_ActiveButLapsedLease_ReturnsConflict(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Status = AccessLeaseStatus.Active;
        existing.NotAfter = _now.AddMinutes(-1);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessLeaseAlreadyUsed>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_WindowNotStarted_ReturnsBadRequest(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.NotBefore = _now.AddHours(1);
        request.NotAfter = _now.AddHours(2);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<ApprovedWindowNotStarted>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_WindowEnded_ReturnsBadRequest(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<ApprovedWindowEnded>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_Approved_MintsLeaseSpanningRequestWindow(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.Minted);

        var result = (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        Assert.Equal(request.Id, result.AccessRequestId);
        Assert.Equal(request.OrganizationId, result.OrganizationId);
        Assert.Equal(request.CollectionId, result.CollectionId);
        Assert.Equal(request.CipherId, result.CipherId);
        Assert.Equal(request.RequesterId, result.RequesterId);
        Assert.Equal(AccessLeaseStatus.Active, result.Status);
        // Activation mints the window the approver approved, not a window anchored at activation time.
        Assert.Equal(request.NotBefore, result.NotBefore);
        Assert.Equal(request.NotAfter, result.NotAfter);
        Assert.Equal(_now, result.CreationDate);
        Assert.NotEqual(default, result.Id);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(result, _now, Arg.Any<bool>());
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(request.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(request.RequesterId);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_LostRace_WinnerLive_ReturnsWinner(AccessRequest request, AccessLease winner)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        winner.Status = AccessLeaseStatus.Active;
        winner.NotAfter = _now.AddMinutes(30);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.PreconditionFailed);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null, winner);

        var result = (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        Assert.Same(winner, result);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_LostRace_NoLiveLease_ReturnsConflict(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.PreconditionFailed);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<AccessRequestNotActivatable>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_SingleActiveLeaseApplies_PassesEnforceTrue_AndMints(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // The constraint binds for this caller and cipher: enforcement must be passed through to the mint.
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>().AppliesAsync(request.RequesterId, request.CipherId)
            .Returns(true);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, true)
            .Returns(AccessLeaseMintOutcome.Minted);

        var result = (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        Assert.Equal(AccessLeaseStatus.Active, result.Status);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(result, _now, true);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_SingleActiveLeaseConflict_ReturnsConflict(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>().AppliesAsync(request.RequesterId, request.CipherId)
            .Returns(true);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, true)
            .Returns(AccessLeaseMintOutcome.SingleActiveLeaseConflict);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<SingleActiveLeaseConflict>(result.AssertError());
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_EscapePathExists_PassesEnforceFalse(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // An escape path leaves the caller unconstrained, so enforcement must be passed as false.
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>().AppliesAsync(request.RequesterId, request.CipherId)
            .Returns(false);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, false)
            .Returns(AccessLeaseMintOutcome.Minted);

        (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, false);
    }

    // The happy path records the activation before and after the mint: an Attempt up front, then a LeaseActivated
    // Outcome once the lease is minted.
    [Theory, BitAutoData]
    public async Task ActivateAsync_Minted_EmitsActivatedAttemptThenOutcome(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.Minted);

        (await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id)).AssertSuccess();

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessRequestId == request.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRequestId == request.Id));
    }

    // A refused activation records the Attempt, then a LeaseActivationRejected Outcome (not LeaseActivated) -- the
    // outcome kind follows the mint result.
    [Theory, BitAutoData]
    public async Task ActivateAsync_SingleActiveLeaseConflict_EmitsAttemptThenRejectedOutcome(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>().AppliesAsync(request.RequesterId, request.CipherId)
            .Returns(true);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, true)
            .Returns(AccessLeaseMintOutcome.SingleActiveLeaseConflict);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.IsType<SingleActiveLeaseConflict>(result.AssertError());

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivationRejected && e.Phase == AccessAuditEventPhase.Outcome));
    }

    private static SutProvider<ActivateAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<ActivateAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    // An approved request owned by its BitAutoData requester, with an open window containing _now and no produced
    // lease. Tests override the specific precondition they exercise.
    private static void SetupApprovedRequest(SutProvider<ActivateAccessRequestCommand> sutProvider, AccessRequest request)
    {
        request.Status = AccessRequestStatus.Approved;
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null);
    }
}
