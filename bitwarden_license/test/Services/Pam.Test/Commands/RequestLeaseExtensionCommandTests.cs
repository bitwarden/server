using Bit.Services.Pam.Engine;
using Bit.Services.Pam.Models;
using Bit.Services.Pam.Models.Conditions;
using Bit.Services.Pam.OrganizationFeatures.Commands;
using Bit.Services.Pam.Services;
using Bit.Core.Exceptions;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
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
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory]
    [BitAutoData(AccessLeaseStatus.Revoked)]
    [BitAutoData(AccessLeaseStatus.Expired)]
    public async Task ExtendAsync_LeaseNotActive_ThrowsConflict(AccessLeaseStatus status, AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.Status = status;

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
        await sutProvider.GetDependency<IAccessRequestRepository>().DidNotReceiveWithAnyArgs()
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_LeaseWindowEnded_ThrowsConflict(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        lease.NotAfter = _now.AddMinutes(-1);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
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
            .CreateApprovedExtensionAsync(default!, default!, default);
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
            .CreateApprovedExtensionAsync(default!, default!, default);
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
            .CreateApprovedExtensionAsync(default!, default!, default);
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_Valid_RecordsApprovedExtensionAndExtendsLeaseInPlace(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
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
        Assert.Equal(lease.NotAfter, result.NotBefore);
        Assert.Equal(expectedNotAfter, result.NotAfter);
        Assert.Equal("incident", result.Reason);
        Assert.Equal(_now, result.ResolvedDate);

        // The repo applies the request + decision + lease bump atomically.
        await sutProvider.GetDependency<IAccessRequestRepository>().Received(1).CreateApprovedExtensionAsync(
            Arg.Is<AccessRequest>(r =>
                r.ExtensionOfLeaseId == lease.Id
                && r.Status == AccessRequestStatus.Approved
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
    public async Task ExtendAsync_RepoReportsLeaseNotActive_ThrowsConflict(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.LeaseNotActive);

        await Assert.ThrowsAsync<ConflictException>(
            () => sutProvider.Sut.ExtendAsync(lease.RequesterId, Submission(lease.Id)));
    }

    [Theory, BitAutoData]
    public async Task ExtendAsync_RepoReportsAlreadyExtended_ThrowsBadRequest(AccessLease lease)
    {
        var sutProvider = Setup();
        SetupExtendableLease(sutProvider, lease);
        // Lost a race: another extension landed between the pre-check and the guarded write.
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.AlreadyExtended);

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
        SutProvider<RequestLeaseExtensionCommand> sutProvider, AccessLease lease, bool allowsExtensions = true)
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
                AllowsExtensions = allowsExtensions,
                MaxExtensionDurationSeconds = _maxExtensionDurationSeconds,
            });

        sutProvider.GetDependency<IAccessRequestRepository>().CountExtensionsByLeaseIdAsync(lease.Id).Returns(0);
        sutProvider.GetDependency<IAccessRequestRepository>()
            .CreateApprovedExtensionAsync(Arg.Any<AccessRequest>(), Arg.Any<AccessDecision>(), _now)
            .Returns(AccessLeaseExtendOutcome.Extended);
    }
}
