using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Errors;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class SubmitAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SubmitAsync_CipherNotAccessible_ReturnsNotFound(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 });

        Assert.IsType<CipherNotFound>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_NotLeasingGated_ReturnsBadRequest(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>().ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 });

        Assert.IsType<CipherNotGated>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Automatic_CreatesApprovedRequestWithoutMintingLease(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId, Guid ruleId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false, ruleId);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = (await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" })).AssertSuccess();

        // The automatic path no longer mints a lease at submit; it produces a startable, already-approved request the
        // requester activates explicitly. The window spans the requested duration from now.
        Assert.Equal(AccessApprovalMode.Automatic, result.ApprovalMode);
        Assert.Equal(AccessRequestStatus.Approved, result.Request.Status);
        Assert.Equal(_now, result.Request.NotBefore);
        Assert.Equal(_now.AddSeconds(3600), result.Request.NotAfter);
        Assert.Equal("deploy", result.Request.Reason);

        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1)
            .CreateAutoApprovedAsync(
                Arg.Is<AccessRequest>(r => r.Status == AccessRequestStatus.Approved && r.NotBefore == _now
                    && r.NotAfter == _now.AddSeconds(3600) && r.RuleId == ruleId),
                Arg.Is<AccessDecision>(d => d.DeciderKind == AccessDeciderKind.Automatic
                    && d.Verdict == AccessDecisionVerdict.Approve));
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticWithWindow_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now, End = _now.AddHours(1) });

        Assert.IsType<DurationExpected>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticMissingDuration_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission());

        Assert.IsType<DurationMustBePositive>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticDurationExceedsMax_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = SubmitAccessRequestCommand.MaxDurationSeconds + 1 });

        Assert.IsType<DurationExceedsMax>(result.AssertError());
    }

    // PM-39858: the rule's own MaxLeaseDurationSeconds was persisted and shown in the admin console but never read at
    // submit, so only the global 24h ceiling applied and an over-cap duration was granted in full.
    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticDurationExceedsRuleMax_ReturnsBadRequestAndCreatesNoRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false,
            maxLeaseDurationSeconds: 900);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 });

        Assert.IsType<DurationExceedsMax>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticDurationEqualToRuleMax_CreatesRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false,
            maxLeaseDurationSeconds: 900);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = (await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 900 })).AssertSuccess();

        Assert.Equal(_now.AddSeconds(900), result.Request.NotAfter);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticRuleCapAboveGlobalCeiling_StillEnforcesGlobalCeiling(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false,
            maxLeaseDurationSeconds: 7 * 24 * 60 * 60);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = SubmitAccessRequestCommand.MaxDurationSeconds + 1 });

        Assert.IsType<DurationExceedsMax>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticPolicyDenied_ReturnsBadRequestAndIssuesNoLease(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupEvaluation(sutProvider, AccessEvaluation.Deny(DenyReason.NotWithinIpRange));

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 });

        Assert.IsType<AccessDeniedByNetwork>(result.AssertError());
        // A rule the caller fails to satisfy must not produce an approved request.
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Human_CreatesPendingRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, Guid ruleId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true, ruleId);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateAsync(Arg.Any<AccessRequest>())
            .Returns(callInfo => callInfo.Arg<AccessRequest>());

        var start = _now.AddHours(1);
        var end = _now.AddHours(2);
        var result = (await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { Start = start, End = end, Reason = "audit" })).AssertSuccess();

        Assert.Equal(AccessApprovalMode.Human, result.ApprovalMode);
        Assert.NotNull(result.Request);
        Assert.Equal(AccessRequestStatus.Pending, result.Request!.Status);
        Assert.Equal(start, result.Request.NotBefore);
        Assert.Equal(end, result.Request.NotAfter);
        Assert.Equal("audit", result.Request.Reason);
        Assert.Equal(ruleId, result.Request.RuleId);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(collectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(userId);
    }

    // The human path pins the window at submit and the approver can only act on what was pinned, so the rule's cap has
    // to be refused here too — not left for the approver to catch.
    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanWindowExceedsRuleMax_ReturnsBadRequestAndCreatesNoRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true,
            maxLeaseDurationSeconds: 1800);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission
        {
            Start = _now.AddHours(1),
            End = _now.AddHours(3),
            Reason = "audit",
        });

        Assert.IsType<WindowExceedsMax>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanWindowWithinRuleMax_CreatesPendingRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true,
            maxLeaseDurationSeconds: 1800);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateAsync(Arg.Any<AccessRequest>())
            .Returns(callInfo => callInfo.Arg<AccessRequest>());

        var result = (await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission
        {
            Start = _now.AddHours(1),
            End = _now.AddHours(1).AddSeconds(1800),
            Reason = "audit",
        })).AssertSuccess();

        Assert.Equal(AccessRequestStatus.Pending, result.Request!.Status);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Automatic_DoesNotNotifyApprovers(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        (await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" })).AssertSuccess();

        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        // The auto path mints no approval gate, but the requester's other devices still learn of the new approved
        // request.
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanMissingReason_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2) });

        Assert.IsType<ReasonRequired>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanWithDuration_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = 3600, Reason = "x" });

        Assert.IsType<WindowExpected>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanStartNotBeforeEnd_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(2), End = _now.AddHours(1), Reason = "x" });

        Assert.IsType<WindowEndBeforeStart>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingActiveLease_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 });

        Assert.IsType<AccessAlreadyActive>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingPendingRequest_ReturnsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessRequest pending)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId)
            .Returns(pending);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2), Reason = "x" });

        Assert.IsType<AccessRequestAlreadyPending>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingApprovedUnactivatedRequest_ReturnsBadRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessRequest approved)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(approved);

        // An approved-but-not-yet-activated request already grants startable access; a second request would stack.
        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2), Reason = "x" });

        Assert.IsType<AccessRequestAlreadyApproved>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    private static SutProvider<SubmitAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<SubmitAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupHumanCreate(SutProvider<SubmitAccessRequestCommand> sutProvider) =>
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateAsync(Arg.Any<AccessRequest>())
            .Returns(callInfo => callInfo.Arg<AccessRequest>());

    private static void SetupCipher(SutProvider<SubmitAccessRequestCommand> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }

    private static void SetupResolution(SutProvider<SubmitAccessRequestCommand> sutProvider, Guid userId, Guid cipherId,
        Guid orgId, Guid collectionId, bool requiresHuman, Guid ruleId = default,
        int? maxLeaseDurationSeconds = null)
    {
        var condition = requiresHuman ? new HumanApprovalCondition() : (AccessCondition)new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] };
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(orgId, collectionId, requiresHuman, [condition])
            {
                RuleId = ruleId,
                MaxLeaseDurationSeconds = maxLeaseDurationSeconds,
            });
    }

    private static void SetupEvaluation(SutProvider<SubmitAccessRequestCommand> sutProvider, AccessEvaluation evaluation)
    {
        sutProvider.GetDependency<IAccessRuleEngine>()
            .Evaluate(Arg.Any<IReadOnlyList<AccessCondition>>(), Arg.Any<AccessSignals>())
            .Returns(evaluation);
    }
}
