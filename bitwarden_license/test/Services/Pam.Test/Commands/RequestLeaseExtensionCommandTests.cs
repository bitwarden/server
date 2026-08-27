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
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

namespace Bit.Services.Pam.Test.Commands;

[SutProviderCustomize]
public class RequestLeaseExtensionCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
    private const int _maxExtensionDurationSeconds = 4 * 60 * 60;

    /// <summary>Pinned rather than shared with the command: the wording is part of what the denial promises.</summary>
    private const string _leaseEndedComment = "The lease being extended has ended";

    [Theory, BitAutoData]
    public async Task ExtendAsync_LeaseMissing_ThrowsNotFound(Guid userId, Guid leaseId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(leaseId).Returns((AccessLease?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ExtendAsync(userId, Submission(leaseId)));
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_NotOwner_ThrowsNotFound(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        // Someone else's lease is indistinguishable from a missing one, so ids can't be probed.
        await Assert.ThrowsAsync<NotFoundException>(() => sutProvider.Sut.ExtendAsync(userId, Submission(lease.Id)));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory]
    [BitAutoData(AccessLeaseAction.Revoked)]
    [BitAutoData(AccessLeaseAction.Cancelled)]
    public async Task ExtendAsync_LeaseNotActive_StillReachesTheGuardedWrite(AccessLeaseAction action, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.Action = action;

        // No pre-check on the lease's liveness: whether there is anything left to extend is settled once, under the
        // lease lock, by the write itself — which answers it with a denied request rather than a refusal (PM-42632).
        await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        await sutProvider.GetDependency<IAccessRequestRepository>().ReceivedWithAnyArgs(1)
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_LeaseWindowEnded_StillReachesTheGuardedWrite(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.NotAfter = _now.AddMinutes(-1);

        await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        await sutProvider.GetDependency<IAccessRequestRepository>().ReceivedWithAnyArgs(1)
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_ItemNotGated_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(lease.RequesterId, lease.CipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_ExtensionsNotAllowed_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease, allowsExtensions: false);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
        Assert.Contains("does not allow extending", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-60)]
    public async Task ExtendAsync_NonPositiveDuration_ThrowsBadRequest(int durationSeconds, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, durationSeconds)));
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_DurationExceedsRuleMax_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId,
                Submission(lease.Id, _maxExtensionDurationSeconds + 1)));
        Assert.Contains("maximum extension length", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ExtendAsync_BlankReason_ThrowsBadRequest(string reason, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, reason: reason)));
        Assert.Contains("justification", ex.Message);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_AlreadyExtended_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        // A lease may be extended once; an existing extension request blocks another.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(lease.Id).Returns(1);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
        Assert.Contains("already been extended", ex.Message);
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_Valid_RecordsApprovedExtensionAndExtendsLeaseInPlace(AccessLease lease, Guid ruleId)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease, ruleId: ruleId);
        const int duration = 2 * 60 * 60;
        var expectedNotAfter = lease.NotAfter.AddSeconds(duration);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, duration, "incident"));

        // Auto-approved extension request, pointing at the parent lease, spanning [old end .. new end].
        Assert.Equal(AccessRequestStatus.Approved, result.Status);
        Assert.Equal(lease.Id, result.ExtensionOfLeaseId);
        Assert.Equal(lease.CipherId, result.CipherId);
        Assert.Equal(lease.OrganizationId, result.OrganizationId);
        Assert.Equal(lease.CollectionId, result.CollectionId);
        Assert.Equal(lease.RequesterId, result.RequesterId);
        Assert.Equal(ruleId, result.RuleId);
        Assert.Equal(lease.NotAfter, result.NotBefore);
        Assert.Equal(expectedNotAfter, result.NotAfter);
        Assert.Equal("incident", result.Reason);
        Assert.Equal(_now, result.ResolvedDate);

        var decision = Assert.Single(result.Decisions);
        Assert.Equal(AccessDeciderKind.Automatic, decision.DeciderKind);
        Assert.Equal(AccessDecisionVerdict.Approve, decision.Verdict);
        Assert.Null(decision.Comment);

        // The repo applies the request + decision + lease bump atomically.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CreateApprovedExtensionAsync(
            Arg.Is<AccessRequest>(r =>
                r.ExtensionOfLeaseId == lease.Id
                && r.Action == AccessRequestAction.Approved
                && r.RuleId == ruleId
                && r.NotBefore == lease.NotAfter
                && r.NotAfter == expectedNotAfter),
            Arg.Is<AccessDecision>(d =>
                d.DeciderKind == AccessDeciderKind.Automatic && d.Verdict == AccessDecisionVerdict.Approve),
            _now,
            Arg.Any<string?>());

        // The widened lease window must reach both the approvers (active-leases / history views) and the requester's
        // other devices (banner / badge countdown).
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(lease.RequesterId);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsLeaseNotActive_ReturnsDeniedExtension(AccessLease lease, Guid ruleId)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease, ruleId: ruleId);
        SetupOutcome(sutProvider, AccessLeaseExtendOutcome.LeaseNotActive);
        const int duration = 2 * 60 * 60;

        var result = await sutProvider.Sut.ExtendAsync(
            lease.RequesterId, Submission(lease.Id, duration, "incident"));

        // The lease ended under the request, so the extension resolves denied rather than failing the call: the
        // requester gets a request they can find, carrying the window they asked for (PM-42632).
        Assert.Equal(AccessRequestStatus.Denied, result.Status);
        Assert.Equal(lease.Id, result.ExtensionOfLeaseId);
        Assert.Equal(lease.NotAfter, result.NotBefore);
        Assert.Equal(lease.NotAfter.AddSeconds(duration), result.NotAfter);
        Assert.Equal("incident", result.Reason);
        Assert.Equal(_now, result.ResolvedDate);

        // The automatic verdict names why, so the requester's history can show it without guessing from the status.
        var decision = Assert.Single(result.Decisions);
        Assert.Equal(AccessDeciderKind.Automatic, decision.DeciderKind);
        Assert.Equal(AccessDecisionVerdict.Deny, decision.Verdict);
        Assert.Equal(_leaseEndedComment, decision.Comment);
        Assert.Null(decision.ApproverId);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsLeaseNotActive_PassesTheDenialCommentToTheWrite(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        SetupOutcome(sutProvider, AccessLeaseExtendOutcome.LeaseNotActive);

        await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        // The comment is the command's to supply — the repository records it on the Deny it writes, so the projection
        // above and the stored decision cannot drift.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CreateApprovedExtensionAsync(
            Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now, _leaseEndedComment);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsLeaseNotActive_AuditsTheDenialAndNotifiesOnlyTheRequester(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        SetupOutcome(sutProvider, AccessLeaseExtendOutcome.LeaseNotActive);

        await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        // The attempt is closed out rather than left in doubt: the outcome carries the denial, against the lease's
        // own (unchanged) end.
        await sutProvider.GetDependency<IAccessAuditEventEmitter>().Received(1).EmitAsync(
            Arg.Is<AccessAuditEventData>(e =>
                e.Kind == AccessAuditEventKind.RequestDenied
                && e.Phase == AccessAuditEventPhase.Outcome
                && e.AccessLeaseId == lease.Id
                && e.LeaseNotAfter == lease.NotAfter
                && e.Detail == _leaseEndedComment));

        // No collection-wide lease state changed, so the approver inbox has nothing to re-fetch; only the
        // requester's own devices need to see the new row.
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1).NotifyRequesterAsync(lease.RequesterId);
        await sutProvider.GetDependency<IApproverInboxNotifier>().DidNotReceiveWithAnyArgs()
            .NotifyCollectionApproversAsync(default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsAlreadyExtended_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        // Lost a race: another extension landed between the pre-check and the guarded write.
        SetupOutcome(sutProvider, AccessLeaseExtendOutcome.AlreadyExtended);

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
        Assert.Contains("already been extended", ex.Message);
    }

    private static AccessLeaseExtensionSubmission Submission(
        Guid leaseId, int durationSeconds = 3600, string? reason = "Investigating an incident") =>
        new() { LeaseId = leaseId, DurationSeconds = durationSeconds, Reason = reason };

    private static SutProvider<RequestLeaseExtensionCommand> Setup()
    {
        var sutProvider = new SutProvider<RequestLeaseExtensionCommand>().WithFakeTimeProvider().Create();
        sutProvider.GetDependency<FakeTimeProvider>().SetUtcNow(_now);
        return sutProvider;
    }

    // An active, in-window lease owned by its BitAutoData requester, governed by an extension-enabled rule with no
    // extension used yet, and a repo that extends successfully. Tests override the precondition they exercise.
    private static void SetupExtendableLease(
        SutProvider<RequestLeaseExtensionCommand> sutProvider, AccessLease lease, bool allowsExtensions = true,
        Guid ruleId = default)
    {
        lease.Action = AccessLeaseAction.None;
        lease.NotAfter = _now.AddHours(1);
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(lease.Id).Returns(lease);

        // A human-approval rule still yields automatic extensions — the approval gate never applies to extensions.
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(lease.RequesterId, lease.CipherId, Arg.Any<AccessSignals>())
            .Returns(new GoverningRule(lease.OrganizationId, lease.CollectionId, RequiresHumanApproval: true,
                [new HumanApprovalCondition()])
            {
                RuleId = ruleId,
                AllowsExtensions = allowsExtensions,
                MaxExtensionDurationSeconds = _maxExtensionDurationSeconds,
            });

        sutProvider.GetDependency<IAccessRequestRepository>().CountExtensionsByLeaseIdAsync(lease.Id).Returns(0);
        SetupOutcome(sutProvider, AccessLeaseExtendOutcome.Extended);
    }

    /// <summary>What the guarded write reports back — the authority on whether there was anything left to extend.</summary>
    private static void SetupOutcome(
        SutProvider<RequestLeaseExtensionCommand> sutProvider, AccessLeaseExtendOutcome outcome) =>
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now, Arg.Any<string?>())
            .Returns(outcome);
}
