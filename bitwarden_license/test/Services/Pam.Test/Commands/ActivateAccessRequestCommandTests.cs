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

    // The caller's source address. In 10.0.0.0/8 and outside 192.168.0.0/16, so the allowlists below read as
    // "still admits them" and "no longer admits them" respectively.
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

        // Someone else's request is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ActivateAsync(userId, request.Id, _now));
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_ExtensionRequest_ThrowsBadRequestWithoutMinting(
        AccessRequest request, Guid parentLeaseId)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // An extension applied in place when it was created and is left Approved with no lease of its own, so every
        // other guard below would pass for it. Only ExtensionOfLeaseId distinguishes it.
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
        // Revoking the parent is what used to make this reachable even under a singleton rule: it clears the only
        // thing that was refusing the mint, letting a revoked requester re-grant themselves the rest of the window.
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

        // A request authorizes access at most once; a revoked or lapsed lease is final.
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

        await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now);

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

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));

        var emitter = sutProvider.GetDependency<IAccessAuditEventEmitter>();
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivated && e.Phase == AccessAuditEventPhase.Attempt));
        await emitter.Received(1).EmitAsync(Arg.Is<AccessAuditEventData>(e =>
            e.Kind == AccessAuditEventKind.LeaseActivationRejected && e.Phase == AccessAuditEventPhase.Outcome));
    }

    // The rule pinned at submit is re-evaluated before the mint, so an approval stays spendable only while the
    // conditions that produced it still hold. Nothing downstream re-asks: CipherLeaseGate releases a gated cipher on
    // the existence of an active lease alone, which makes this the last gate (PM-42273).

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
        // The allowlist the approval was granted under has been narrowed to a range the caller is no longer in --
        // equivalently, the caller has moved off the network it admits. Either way the lease must not be minted.
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
        // Held to the rule that approved it: re-deriving which rule governs the cipher today would let a rule created
        // or re-pointed since submit take over from the one the request was decided under.
        await sutProvider.GetDependency<IGoverningRuleResolver>().DidNotReceiveWithAnyArgs()
            .ResolveAsync(default, default, default!);
    }

    [Theory, BitAutoData]
    public async Task ActivateAsync_HumanApprovedRequest_StillReEvaluatesTheRulesOtherConditions(AccessRequest request)
    {
        var sutProvider = Setup();
        SetupApprovedRequest(sutProvider, request);
        // A human-gated rule carrying an IP allowlist. The approver settled the approval gate; the allowlist is a
        // standing condition on the network the credential is reached from, so it is re-asked here. An approver
        // decides *who* may have access, not from where.
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
        // The gate is stripped before evaluation, leaving nothing to evaluate. Folding it back in would return
        // requires-approval and refuse every human-approved activation -- there is no second approver to route to.
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
        // The resolver could not parse the stored document and substituted its fail-safe approval gate. Stripping that
        // gate leaves an empty list, which the engine reads as vacuously satisfied, so deferring to the conditions
        // here would turn the fail-safe into a fail-open on exactly the rules the server cannot understand.
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
        // The admin disabled or deleted the rule. Leasing has stopped governing the credential, so there is no
        // condition left to hold the caller to and the approval they already have activates.
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
        // Rows written before RuleId existed carry no pin. Falling back to resolution keeps them behind the gate
        // rather than waving through every request already in flight when this shipped.
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

        // The re-check gates minting, not access: the lease already exists, and taking it back is revocation's job,
        // not something a repeat activation should do behind the caller's back.
        Assert.Same(existing, await sutProvider.Sut.ActivateAsync(request.RequesterId, request.Id, _now));
    }

    // A refused activation is recorded like the other refusals -- the Attempt, then a LeaseActivationRejected Outcome
    // carrying the reason, so an admin can see that someone tried to start access the rule no longer admits.
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
        // No TimeProvider: the command takes the caller's clock as a parameter (every call here passes _now), so the
        // response can be derived against the same instant that guarded and minted the lease.
        return new SutProvider<ActivateAccessRequestCommand>()
            // The real engine, not a stub: these tests turn on how an IP allowlist actually evaluates against a
            // caller's address, and a stubbed verdict would only assert that the command forwards what it is told.
            .SetDependency<IAccessRuleEngine>(new AccessRuleEngine())
            .Create();
    }

    // An approved request owned by its BitAutoData requester, with an open window containing _now, a pinned rule, and
    // no produced lease. The caller reaches the API from _requesterIp. Tests override the specific precondition they
    // exercise; those that leave the resolver unstubbed resolve no rule, which is the ungated case.
    private static void SetupApprovedRequest(SutProvider<ActivateAccessRequestCommand> sutProvider, AccessRequest request)
    {
        request.Action = AccessRequestAction.Approved;
        // BitAutoData fills every nullable, ExtensionOfLeaseId included. An extension is refused outright, so a
        // fixture left as generated models the one request shape that never activates -- pin it null here so these
        // tests exercise a plain approved request, and set it explicitly in the tests that are about extensions.
        request.ExtensionOfLeaseId = null;
        request.NotBefore = _now.AddMinutes(-5);
        request.NotAfter = _now.AddHours(1);
        request.RuleId = Guid.NewGuid();
        sutProvider.GetDependency<IAccessRequestRepository>().GetByIdAsync(request.Id).Returns(request);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByAccessRequestIdAsync(request.Id)
            .Returns((AccessLease?)null);
        sutProvider.GetDependency<ICurrentContext>().IpAddress.Returns(_requesterIp);
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
