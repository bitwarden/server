using Bit.Core.Exceptions;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

[SutProviderCustomize]
public class ActivateAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task ActivateAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ActivateAsync(userId, requestId));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_NotOwner_ThrowsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);

        // Someone else's request is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ActivateAsync(userId, request.Id));
    }

    [Theory]
    [BitAutoData(AccessRequestStatus.Pending)]
    [BitAutoData(AccessRequestStatus.Denied)]
    [BitAutoData(AccessRequestStatus.Cancelled)]
    [BitAutoData(AccessRequestStatus.ExpiredUnanswered)]
    public async Task ActivateAsync_NotApproved_ThrowsConflict(AccessRequestStatus status, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.Status = status;

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
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

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.Same(existing, result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory]
    [BitAutoData(AccessLeaseStatus.Revoked)]
    [BitAutoData(AccessLeaseStatus.Expired)]
    public async Task ActivateAsync_AlreadyActivated_DeadLease_ThrowsConflict(
        AccessLeaseStatus leaseStatus, AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Status = leaseStatus;
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        // A request authorizes access at most once; a revoked or lapsed lease is final.
        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_ActiveButLapsedLease_ThrowsConflict(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Status = AccessLeaseStatus.Active;
        existing.NotAfter = _now.AddMinutes(-1);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_WindowNotStarted_ThrowsBadRequest(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.NotBefore = _now.AddHours(1);
        request.NotAfter = _now.AddHours(2);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
        Assert.Contains("not started", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_WindowEnded_ThrowsBadRequest(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.NotBefore = _now.AddHours(-2);
        request.NotAfter = _now.AddHours(-1);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
        Assert.Contains("already ended", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_Approved_MintsLeaseSpanningRequestWindow(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.Minted);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

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

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.Same(winner, result);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_LostRace_NoLiveLease_ThrowsConflict(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.PreconditionFailed);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
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

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        Assert.Equal(AccessLeaseStatus.Active, result.Status);
        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(result, _now, true);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_SingleActiveLeaseConflict_ThrowsConflict(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>().AppliesAsync(request.RequesterId, request.CipherId)
            .Returns(true);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, true)
            .Returns(AccessLeaseMintOutcome.SingleActiveLeaseConflict);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id));
        Assert.Contains("Another active lease exists", ex.Message);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
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

        await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id);

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, false);
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
