using Bit.Core.Exceptions;
using Bit.Core.Pam.Engine;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Models.Conditions;
using Bit.Core.Pam.OrganizationFeatures.Commands;
using Bit.Core.Pam.Repositories;
using Bit.Core.Pam.Services;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Pam.Commands;

[SutProviderCustomize]
public class SubmitAccessRequestCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 4, 12, 0, 0, DateTimeKind.Utc);

    [Theory, BitAutoData]
    public async Task SubmitAsync_CipherNotAccessible_ThrowsNotFound(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<ICipherRepository>().GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_NotLeasingGated_ThrowsBadRequest(Guid userId, Guid cipherId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        sutProvider.GetDependency<IGoverningRuleResolver>().ResolveAsync(userId, cipherId)
            .Returns((GoverningRule?)null);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("does not require a lease", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Automatic_CreatesApprovedRequestWithoutMintingLease(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" });

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
                    && r.NotAfter == _now.AddSeconds(3600)),
                Arg.Is<AccessDecision>(d => d.DeciderKind == AccessDeciderKind.Automatic
                    && d.Verdict == AccessDecisionVerdict.Approve));
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticWithWindow_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now, End = _now.AddHours(1) }));
        Assert.Contains("provide a duration", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticMissingDuration_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission()));
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticDurationExceedsMax_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = SubmitAccessRequestCommand.MaxDurationSeconds + 1 }));
        Assert.Contains("maximum", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_AutomaticPolicyDenied_ThrowsBadRequestAndIssuesNoLease(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupEvaluation(sutProvider, AccessEvaluation.Deny(DenyReason.NotWithinIpRange));

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("network", ex.Message);
        // A rule the caller fails to satisfy must not produce an approved request.
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Human_CreatesPendingRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateAsync(Arg.Any<AccessRequest>())
            .Returns(callInfo => callInfo.Arg<AccessRequest>());

        var start = _now.AddHours(1);
        var end = _now.AddHours(2);
        var result = await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { Start = start, End = end, Reason = "audit" });

        Assert.Equal(AccessApprovalMode.Human, result.ApprovalMode);
        Assert.NotNull(result.Request);
        Assert.Equal(AccessRequestStatus.Pending, result.Request!.Status);
        Assert.Equal(start, result.Request.NotBefore);
        Assert.Equal(end, result.Request.NotAfter);
        Assert.Equal("audit", result.Request.Reason);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAutoApprovedAsync(default!, default!);
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(collectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_Automatic_DoesNotNotifyApprovers(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        SetupEvaluation(sutProvider, AccessEvaluation.Allow);

        await sutProvider.Sut.SubmitAsync(userId, cipherId,
            new AccessRequestSubmission { DurationSeconds = 3600, Reason = "deploy" });

        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
        // The auto path mints no approval gate, but the requester's other devices still learn of the new approved
        // request.
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanMissingReason_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2) }));
        Assert.Contains("reason is required", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanWithDuration_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { DurationSeconds = 3600, Reason = "x" }));
        Assert.Contains("requires human approval", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_HumanStartNotBeforeEnd_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(2), End = _now.AddHours(1), Reason = "x" }));
        Assert.Contains("before the end date", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingActiveLease_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: false);
        sutProvider.GetDependency<IAccessLeaseRepository>()
            .GetActiveByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(lease);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId, new AccessRequestSubmission { DurationSeconds = 3600 }));
        Assert.Contains("already have active access", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingPendingRequest_ThrowsBadRequest(Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessRequest pending)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActivePendingByRequesterIdCipherIdAsync(userId, cipherId)
            .Returns(pending);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2), Reason = "x" }));
        Assert.Contains("already have a pending request", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task SubmitAsync_ExistingApprovedUnactivatedRequest_ThrowsBadRequest(
        Guid userId, Guid cipherId, Guid orgId, Guid collectionId, AccessRequest approved)
    {
        var sutProvider = Setup();
        SetupCipher(sutProvider, userId, cipherId);
        SetupResolution(sutProvider, userId, cipherId, orgId, collectionId, requiresHuman: true);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .GetActiveApprovedByRequesterIdCipherIdAsync(userId, cipherId, _now)
            .Returns(approved);

        // An approved-but-not-yet-activated request already grants startable access; a second request would stack.
        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SubmitAsync(userId, cipherId,
                new AccessRequestSubmission { Start = _now.AddHours(1), End = _now.AddHours(2), Reason = "x" }));
        Assert.Contains("already have an approved request", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateAsync(default!);
    }

    private static SutProvider<SubmitAccessRequestCommand> Setup()
    {
        var sutProvider = new SutProvider<SubmitAccessRequestCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    private static void SetupCipher(SutProvider<SubmitAccessRequestCommand> sutProvider, Guid userId, Guid cipherId)
    {
        sutProvider.GetDependency<ICipherRepository>()
            .GetByIdAsync(cipherId, userId)
            .Returns(new CipherDetails { Id = cipherId });
    }

    private static void SetupResolution(SutProvider<SubmitAccessRequestCommand> sutProvider, Guid userId, Guid cipherId,
        Guid orgId, Guid collectionId, bool requiresHuman)
    {
        var condition = requiresHuman ? new HumanApprovalCondition() : (AccessCondition)new IpAllowlistCondition { Cidrs = ["10.0.0.0/8"] };
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(userId, cipherId)
            .Returns(new GoverningRule(orgId, collectionId, requiresHuman, [condition]));
    }

    private static void SetupEvaluation(SutProvider<SubmitAccessRequestCommand> sutProvider, AccessEvaluation evaluation)
    {
        sutProvider.GetDependency<IAccessRuleEngine>()
            .Evaluate(Arg.Any<IReadOnlyList<AccessCondition>>(), Arg.Any<AccessSignals>())
            .Returns(evaluation);
    }
}
