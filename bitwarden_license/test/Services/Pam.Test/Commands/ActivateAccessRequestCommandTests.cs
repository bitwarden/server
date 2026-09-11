using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class ActivateAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

    // In 10.0.0.0/8 and outside 192.168.0.0/16.
    private const string _requesterIp = "10.0.0.5";

    [Theory, BitAutoData]
    public async Task ActivateAsync_RequestMissing_ThrowsNotFound(Guid userId, Guid requestId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(requestId).Returns((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ActivateAsync(userId, requestId, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_NotOwner_ThrowsNotFound(Guid userId, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);

        // A request owned by another user is indistinguishable from a missing one.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ActivateAsync(userId, request.Id, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_Unlicensed_ThrowsBadRequestWithoutMinting(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Licensed at approval, withdrawn since; activation is the last point entitlement can decide.
        sutProvider.GetDependency<ICurrentContext>().AccessPam(request.OrganizationId).Returns(false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        Assert.Contains("Privileged Controls license is required", ex.Message);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_ExtensionRequest_ThrowsBadRequestWithoutMinting(
        AccessRequest request, Guid parentLeaseId)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // An extension is left Approved with no lease of its own; only ExtensionOfLeaseId distinguishes it.
        request.ExtensionOfLeaseId = parentLeaseId;

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_ExtensionRequest_ParentRevoked_StillThrowsWithoutMinting(
        AccessRequest request, Guid parentLeaseId)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.ExtensionOfLeaseId = parentLeaseId;
        // Revoking the parent clears the only thing refusing the mint.
        sutProvider.GetDependency<ISingleActiveLeaseEvaluator>()
            .AppliesAsync(request.RequesterId, request.CipherId).Returns(true);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory]
    [BitAutoData(AccessRequestAction.None)]
    [BitAutoData(AccessRequestAction.Denied)]
    [BitAutoData(AccessRequestAction.Cancelled)]
    public async Task ActivateAsync_NotApproved_ThrowsConflict(AccessRequestAction action, AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.Action = action;

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_LiveLease_ReturnsExistingWithoutMinting(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Action = AccessLeaseAction.None;
        existing.NotAfter = _now.AddMinutes(30);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Same(existing, result);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory]
    [BitAutoData(AccessLeaseAction.Revoked)]
    [BitAutoData(AccessLeaseAction.Cancelled)]
    public async Task ActivateAsync_AlreadyActivated_DeadLease_ThrowsConflict(
        AccessLeaseAction leaseAction, AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Action = leaseAction;
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        // A revoked or lapsed lease is final.
        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_ActiveButLapsedLease_ThrowsConflict(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Action = AccessLeaseAction.None;
        existing.NotAfter = _now.AddMinutes(-1);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_WindowNotStarted_ThrowsBadRequest(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        request.NotBefore = _now.AddHours(1);
        request.NotAfter = _now.AddHours(2);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
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
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
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

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Equal(request.Id, result.AccessRequestId);
        Assert.Equal(request.OrganizationId, result.OrganizationId);
        Assert.Equal(request.CollectionId, result.CollectionId);
        Assert.Equal(request.CipherId, result.CipherId);
        Assert.Equal(request.RequesterId, result.RequesterId);
        Assert.Equal(AccessLeaseAction.None, result.Action);
        // Lease starts at activation, never backdated to the approved window's start.
        Assert.Equal(_now, result.NotBefore);
        Assert.NotEqual(request.NotBefore, result.NotBefore);
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
        winner.Action = AccessLeaseAction.None;
        winner.NotAfter = _now.AddMinutes(30);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.PreconditionFailed);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null, winner);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Same(winner, result);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
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
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
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

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Equal(AccessLeaseAction.None, result.Action);
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
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        Assert.Contains("Another active lease exists", ex.Message);
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

        await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        await sutProvider.GetDependency<IAccessLeaseRepository>().Received(1)
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, false);
    }

    // Attempt up front, then a LeaseActivated Outcome after the mint.
    [Theory, BitAutoData]
    public async Task ActivateAsync_Minted_EmitsActivatedAttemptThenOutcome(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(AccessLeaseMintOutcome.Minted);

        await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt
            && e.AccessRequestId == request.Id));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessRequestId == request.Id));
    }

    // Outcome kind follows the mint result.
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

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivationRejected && e.Phase == AccessAuditEventPhase.Outcome));
    }

    // The rule pinned at submit is re-evaluated before the mint; nothing downstream re-asks.
    [Theory, BitAutoData]
    public async Task ActivateAsync_PinnedRuleStillAdmitsCaller_Mints(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        SetupPinnedRule(sutProvider, request, new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] });
        SetupMint(sutProvider, AccessLeaseMintOutcome.Minted);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Equal(AccessLeaseAction.None, result.Action);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_IpAllowlistNarrowedSinceApproval_ThrowsBadRequestWithoutMinting(
        AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Allowlist narrowed since approval to a range the caller is no longer in.
        SetupPinnedRule(sutProvider, request, new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        Assert.Contains("current network", ex.Message);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        await sutProvider.GetDependency<IRequesterNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyRequesterAsync(default);
        // Held to the rule that approved it, not whatever rule governs the cipher today.
        await sutProvider.GetDependency<IGoverningRuleResolver>().DidNotReceiveWithAnyArgs()
            .ResolveAsync(default, default, default!);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_HumanApprovedRequest_StillReEvaluatesTheRulesOtherConditions(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Approval gates who; the IP allowlist is a standing condition re-asked here.
        SetupPinnedRule(
            sutProvider, request,
            new HumanApprovalCondition(),
            new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        Assert.Contains("current network", ex.Message);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_HumanApprovalGateAlone_DoesNotBlockActivation(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Gate is stripped before evaluation; there is no second approver to route to.
        SetupPinnedRule(sutProvider, request, new HumanApprovalCondition());
        SetupMint(sutProvider, AccessLeaseMintOutcome.Minted);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Equal(AccessLeaseAction.None, result.Action);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_PinnedRuleConditionsUnreadable_ThrowsBadRequestWithoutMinting(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Unreadable conditions get the fail-safe approval gate; ConditionsUnreadable marks it.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolvePinnedAsync(request.RuleId!.Value, request.CollectionId)
            .Returns(new GoverningRule(request.OrganizationId, request.CollectionId, true, [new HumanApprovalCondition()])
            {
                ConditionsUnreadable = true,
            });

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_PinnedRuleNoLongerGoverns_Mints(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Rule disabled or deleted: no condition left to hold the caller to.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolvePinnedAsync(request.RuleId!.Value, request.CollectionId)
            .Returns((GoverningRule?)null);
        SetupMint(sutProvider, AccessLeaseMintOutcome.Minted);

        var result = await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

        Assert.Equal(AccessLeaseAction.None, result.Action);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_RequestPredatesRulePinning_FallsBackToResolvingTheCipher(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // Rows written before RuleId existed carry no pin; falls back to resolution.
        request.RuleId = null;
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(request.RequesterId, request.CipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(
                request.OrganizationId, request.CollectionId, false,
                [new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] }]));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        Assert.Contains("current network", ex.Message);
        await sutProvider.GetDependency<IAccessLeaseRepository>().DidNotReceiveWithAnyArgs()
            .CreateFromApprovedRequestAsync(default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_AlreadyActivated_LiveLease_DoesNotReEvaluate(
        AccessRequest request, AccessLease existing)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        existing.Action = AccessLeaseAction.None;
        existing.NotAfter = _now.AddMinutes(30);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id).Returns(existing);
        SetupPinnedRule(sutProvider, request, new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        // Re-check gates minting, not access; taking back an existing lease is revocation's job.
        Assert.Same(existing, await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_ConditionsNoLongerAdmitCaller_EmitsAttemptThenRejectedOutcome(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        SetupPinnedRule(sutProvider, request, new IpAllowlistCondition { Cidrs = ["192.168.0.0/16"] });

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivationRejected && e.Phase == AccessAuditEventPhase.Outcome
            && e.AccessLeaseId == null && e.Detail == nameof(DenyReason.NotWithinIpRange)));
    }

    private static SutProvider<ActivateAccessRequestCommand> Setup()
    {
        // No TimeProvider: the command takes the caller's clock as a parameter.
        return new SutProvider<ActivateAccessRequestCommand>()
            // Real engine, not a stub: these tests exercise actual IP allowlist evaluation.
            .SetDependency<IAccessRuleEngine>(new AccessRuleEngine())
            .Create();
    }

    // Approved request with an open window containing _now, a pinned rule, and no produced lease.
    private static void SetupApprovedRequest(SutProvider<ActivateAccessRequestCommand> sutProvider, AccessRequest request)
    {
        request.Action = AccessRequestAction.Approved;
        // Extensions are refused outright; pin null so this models a plain approved request.
        request.ExtensionOfLeaseId = null;
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
        request.RuleId = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(_requesterIp);
        // Licensed by default; the licensing tests override it.
        sutProvider.GetDependency<ICurrentContext>().AccessPam(request.OrganizationId).Returns(true);
    }

    private static void SetupPinnedRule(
        SutProvider<ActivateAccessRequestCommand> sutProvider, AccessRequest request, params AccessCondition[] conditions)
    {
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolvePinnedAsync(request.RuleId!.Value, request.CollectionId)
            .Returns(new GoverningRule(
                request.OrganizationId,
                request.CollectionId,
                conditions.Any(c => c is HumanApprovalCondition),
                conditions));
    }

    private static void SetupMint(
        SutProvider<ActivateAccessRequestCommand> sutProvider, AccessLeaseMintOutcome outcome)
    {
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .CreateFromApprovedRequestAsync(Arg.Any<AccessLease>(), _now, Arg.Any<bool>())
            .Returns(outcome);
    }
}
