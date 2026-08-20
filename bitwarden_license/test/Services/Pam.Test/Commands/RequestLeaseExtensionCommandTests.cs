using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Bit.Services.Pam.Engine;
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
public class RequestLeaseExtensionCommandTests
{
    private static readonly DateTime _now = new(2026, 6, 12, 12, 0, 0, DateTimeKind.Utc);
    private const int _maxExtensionDurationSeconds = 4 * 60 * 60;

    [Theory, BitAutoData]
    public async Task ExtendAsync_LeaseMissing_ReturnsNotFound(Guid userId, Guid leaseId)
    {
        var sutProvider = Setup();
        sutProvider.GetDependency<IAccessLeaseRepository>().GetByIdAsync(leaseId).Returns((AccessLease?)null);

        var result = await sutProvider.Sut.ExtendAsync(userId, Submission(leaseId));

        Assert.IsType<AccessLeaseNotFound>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_NotOwner_ReturnsNotFound(Guid userId, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        // Someone else's lease is indistinguishable from a missing one, so ids can't be probed.
        var result = await sutProvider.Sut.ExtendAsync(userId, Submission(lease.Id));

        Assert.IsType<AccessLeaseNotFound>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessLeaseStatus.Revoked)]
    [BitAutoData(AccessLeaseStatus.Expired)]
    public async Task ExtendAsync_LeaseNotActive_ReturnsConflict(AccessLeaseStatus status, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.Status = status;

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<AccessLeaseNoLongerActive>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_LeaseWindowEnded_ReturnsConflict(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.NotAfter = _now.AddMinutes(-1);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<AccessLeaseNoLongerActive>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_ItemNotGated_ReturnsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        sutProvider.GetDependency<IGoverningRuleResolver>()
            .ResolveAsync(lease.RequesterId, lease.CipherId, Arg.Any<AccessSignals>())
            .Returns((GoverningRule?)null);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<CipherNotGated>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_ExtensionsNotAllowed_ReturnsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease, allowsExtensions: false);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<ExtensionsNotAllowed>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(0)]
    [BitAutoData(-60)]
    public async Task ExtendAsync_NonPositiveDuration_ReturnsBadRequest(int durationSeconds, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, durationSeconds));

        Assert.IsType<DurationMustBePositive>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_DurationExceedsRuleMax_ReturnsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId,
                Submission(lease.Id, _maxExtensionDurationSeconds + 1));

        Assert.IsType<ExtensionExceedsMax>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData("")]
    [BitAutoData("   ")]
    public async Task ExtendAsync_BlankReason_ReturnsBadRequest(string reason, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, reason: reason));

        Assert.IsType<ExtensionReasonRequired>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_AlreadyExtended_ReturnsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        // A lease may be extended once; an existing extension request blocks another.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CountExtensionsByLeaseIdAsync(lease.Id).Returns(1);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<AccessLeaseAlreadyExtended>(result.AssertError());
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_Valid_RecordsApprovedExtensionAndExtendsLeaseInPlace(AccessLease lease, Guid ruleId)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease, ruleId: ruleId);
        const int duration = 2 * 60 * 60;
        var expectedNotAfter = lease.NotAfter.AddSeconds(duration);

        var result = (await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id, duration, "incident"))).AssertSuccess();

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

        // The repo applies the request + decision + lease bump atomically.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CreateApprovedExtensionAsync(
            Arg.Is<AccessRequest>(r =>
                r.ExtensionOfLeaseId == lease.Id
                && r.Status == AccessRequestStatus.Approved
                && r.RuleId == ruleId
                && r.NotBefore == lease.NotAfter
                && r.NotAfter == expectedNotAfter),
            Arg.Is<AccessDecision>(d =>
                d.DeciderKind == AccessDeciderKind.Automatic && d.Verdict == AccessDecisionVerdict.Approve),
            _now);

        // The widened lease window must reach both the approvers (active-leases / history views) and the requester's
        // other devices (banner / badge countdown).
        await sutProvider.GetDependency<IApproverInboxNotifier>().Received(1)
            .NotifyCollectionApproversAsync(lease.CollectionId);
        await sutProvider.GetDependency<IRequesterNotifier>().Received(1)
            .NotifyRequesterAsync(lease.RequesterId);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsLeaseNotActive_ReturnsConflict(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.LeaseNotActive);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<AccessLeaseNoLongerActive>(result.AssertError());
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsAlreadyExtended_ReturnsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        // Lost a race: another extension landed between the pre-check and the guarded write.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.AlreadyExtended);

        var result = await sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id));

        Assert.IsType<AccessLeaseAlreadyExtended>(result.AssertError());
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
        lease.Status = AccessLeaseStatus.Active;
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
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.Extended);
    }
}
